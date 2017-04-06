namespace MUDT.IO

  open System
  open System.IO
  open MUDT.Cryptography
  open MUDT.IO.InMemory
  open System.Threading

  type MMFPartitionState =
    {
      mutable partitionHash : HashState;
      fileStream : FileStream;
      isDisposed : bool;
      writerLock : ReaderWriterLockSlim ref
      startPosition : int64;
      currentPosition : int64;
      endPosition : int64;
      partitionSize : int64;
      buffer : MemoryBuffer;
      bufferCapacity : int64;
      bufferLength : int64;
      bytesReadCounter : int64;
      bytesWrittenCounter : int64;
    }

    member x.PrintInfo() =
      let bytesCounter =
        if x.fileStream.CanRead then
          sprintf "BytesRead => %d" x.bytesReadCounter
        else
          sprintf "BytesWritten => %d" x.bytesWrittenCounter
      let res = String.Join("\n", [|
                                    sprintf "StartPosition => %d" x.startPosition;
                                    sprintf "CurrentPosition => %d" x.currentPosition;
                                    sprintf "EndPosition => %d" x.endPosition;
                                    sprintf "Buffer Length => %d" x.bufferLength;
                                    sprintf "Buffer Capacity => %d" x.bufferCapacity;
                                    sprintf "Partition Size => %d" x.partitionSize;
                                    bytesCounter
                                  |])
      printfn "%s" res

  type MMFPartitionStateConfig =
    {
      hashStateConfig : HashStateConfig;
      fs : FileStream;
      sharedLock : ReaderWriterLockSlim ref;
      startPos : int64;
      size : int64;
      bufferCapacity : int64;
    }

  module MMFPartition =

    let createMMFPartitionState (config:MMFPartitionStateConfig) =
      let memBuffer = MemoryBuffer(int(config.bufferCapacity))
      config.fs.Seek(config.startPos, SeekOrigin.Begin) |> ignore
      {
        partitionHash = Hasher.createHashState(config.hashStateConfig);
        fileStream = config.fs;
        isDisposed = false;
        writerLock = config.sharedLock;
        startPosition = config.startPos;
        currentPosition = config.startPos;
        endPosition = config.size + config.startPos - 1L;
        partitionSize = config.size;
        buffer = memBuffer;
        bufferCapacity = config.bufferCapacity;
        bufferLength = 0L;
        bytesReadCounter = 0L;
        bytesWrittenCounter = 0L;
      }

    open MUDT.Utilities.ConversionUtility
    let private printAsString (bytes:byte[]) =
      let str = bytesToUTF8 bytes
      printfn "%s" str
      bytes

    let private updateHash (state:byref<MMFPartitionState>) (bytes:byte[]) =
      state.partitionHash <- Hasher.computeHash state.partitionHash bytes
      bytes

    open MUDT.Utilities.TypeUtility

    let private asyncRead (state:MMFPartitionState) (fromBuffer:bool) (count:int) =
      async {
        let mutable bytes = [||]
        let mutable state' = state
        if fromBuffer then
          bytes <- state'.buffer.ReadBytes(count)
        else
          bytes <- nullByteArray count
          state'.fileStream.Read(bytes, 0, count) |> ignore
          state' <- { state' with bytesReadCounter = state'.bytesReadCounter + int64(count) }
        return bytes, state'
      }

    let private asyncWrite (state:MMFPartitionState) (toBuffer:bool) (bytes:byte[]) =
      async {
        let mutable state' = state
        if toBuffer then
          state.buffer.WriteBytes(bytes)
        else
          state.writerLock.Value.EnterWriteLock()
          state.fileStream.Write(bytes, 0, (Array.length bytes)) |> ignore
          state.fileStream.Flush()
          state' <- { state' with bytesWrittenCounter = state'.bytesWrittenCounter + int64((Array.length bytes)) }
          state.writerLock.Value.ExitWriteLock()
        return state'
      }

    let private copyFromFileAsync (stat:MMFPartitionState) (numBytes:int) =
      async {
        let mutable state = stat
        let doAsyncRead() =
          let (bytes, state') = Async.RunSynchronously(asyncRead state false numBytes)
          state <- state'
          bytes
        state <-
          ()
          |> doAsyncRead
          |> updateHash &state
          |> asyncWrite state true
          |> Async.RunSynchronously
        return { state with currentPosition = state.currentPosition + int64(numBytes); 
                            bufferLength = state.bufferLength + int64(numBytes) }
      }

    let private refillBufferAsync (stat:MMFPartitionState) =
      async {
        let mutable state = stat
        if state.bufferLength < (state.bufferCapacity / 2L) then
          let mutable diff = state.bufferCapacity - state.bufferLength
          if diff + state.currentPosition > (state.partitionSize + state.startPosition) then
            diff <- (state.partitionSize + state.startPosition) - state.currentPosition
          if diff = 0L then 
            return state // if there is nothing left, just return
          else
            return! copyFromFileAsync state (int(diff))
        else // if we're not ready to refill, just return
          return state
      }

    let private flushAsync (len:int) (stat:MMFPartitionState) : Async<MMFPartitionState> =
      async {
        let mutable state = stat
        let doAsyncRead() =
          let (bytes, state') = Async.RunSynchronously(asyncRead state true len)
          state <- state'
          bytes
        state <-
          ()
          |> doAsyncRead
          |> updateHash &state
          |> asyncWrite state false
          |> Async.RunSynchronously
        return { state with currentPosition = state.currentPosition + int64(len); 
                            bufferLength = state.bufferLength - int64(len) }
      }

    let private partialFlushBufferAsync (state:MMFPartitionState) = 
      async {
        if state.bufferLength > (state.bufferCapacity / 2L) then
          let len = state.bufferLength
          return! flushAsync (int(len)) state
        else
          return state
      }

    let fullFlushBufferAsync (state:MMFPartitionState) =
      async {
        if state.bufferLength > 0L then
          let len = state.bufferLength
          return! flushAsync (int(len)) state
        else
          state.fileStream.Flush()
          return state
      }

    let writeToBufferAsync (state:MMFPartitionState) (bytes:byte[]) =
      async {
        do! state.buffer.AsyncWrite(bytes)
        return! partialFlushBufferAsync { state with bufferLength = state.bufferLength + int64(Array.length bytes) }
      }

    let writeStraightToFileAsync (stat:MMFPartitionState) (bytes:byte[]) =
      async {
        let mutable state = stat
        state <-
          bytes
          |> updateHash &state
          |> asyncWrite state false
          |> Async.RunSynchronously
        return { state with currentPosition = state.currentPosition + int64(Array.length bytes); }
      }

    // fixed condition so that `readFromBufferAsync` will continue to return bytes until buffer is empty
    let feop (state:MMFPartitionState) =
      state.partitionSize = (state.currentPosition - state.startPosition) && state.bufferLength = 0L

    let readFromBufferAsync (state:MMFPartitionState) (count:int) =
      async {
        let take = 
          [| state.bufferLength; int64(count)|]
          |> Array.min |> int // count is guaranteed to be int, so no value will exceed size of int
        if feop(state) then return ([||], state) // redundancy check
        else
          let! newState = refillBufferAsync state
          let! bytes, ns = asyncRead newState true take
          return (bytes, { ns with bufferLength = ns.bufferLength - int64(take) })
      }
    let initializeReadBufferAsync (state:MMFPartitionState) =
      copyFromFileAsync state (int(state.bufferCapacity))

    let dispose (state:MMFPartitionState) =
      state.fileStream.Dispose()
      { state with isDisposed = true }