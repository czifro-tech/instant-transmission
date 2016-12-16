namespace MUDT.IO

  open System
  open System.IO
  open MUDT.Cryptography

  type MMFPartitionState =
    {
      mutable partitionHash : HashState;
      fileStream : FileStream;
      startPosition : int64;
      currentPosition : int64;
      partitionSize : int64;
      buffer : MemoryStream;
      bufferCapacity : int64;
    }

  module MMFPartition =

    let createMMFPartitionState (hashStateConfig:HashStateConfig) (fs:FileStream) (startPos:int64) (size:int64) (bufferCapacity:int64) =
      let memStream = new MemoryStream()
      memStream.Capacity <- int(bufferCapacity)
      fs.Seek(startPos, SeekOrigin.Begin) |> ignore
      {
        partitionHash = Hasher.createHashState(hashStateConfig);
        fileStream = fs;
        startPosition = startPos;
        currentPosition = startPos;
        partitionSize = size;
        buffer = memStream;
        bufferCapacity = bufferCapacity;
      }

    let private updateHash (state:byref<MMFPartitionState>) (bytes:byte[]) =
      state.partitionHash <- Hasher.computeHash state.partitionHash bytes
      bytes

    let private refillBufferAsync (stat:MMFPartitionState) =
      async {
        let mutable state = stat
        if state.buffer.Length < (state.bufferCapacity / 2L) then
          let mutable diff = state.bufferCapacity - state.buffer.Length
          if diff + state.currentPosition > state.partitionSize then
            diff <- state.partitionSize - state.currentPosition
          do! (state.fileStream.AsyncRead(int(diff))
          |> Async.RunSynchronously
          |> updateHash &state
          |> state.buffer.AsyncWrite)
          return { state with currentPosition = state.currentPosition + diff }
        else
          return state
      }

    let private flushAsync (len:int) (stat:MMFPartitionState) : Async<MMFPartitionState> =
      async {
        let mutable state = stat
        do! (state.buffer.AsyncRead(len)
        |> Async.RunSynchronously
        |> updateHash &state
        |> state.fileStream.AsyncWrite)
        return { state with currentPosition = state.currentPosition + int64(len) }
      }

    let private partialFlushBufferAsync (state:MMFPartitionState) = 
      async {
        if state.buffer.Length > (state.bufferCapacity / 2L) then
          let len = state.buffer.Length
          return! flushAsync (int(len)) state
        else
          return state
      }

    let fullFlushBufferAsync (state:MMFPartitionState) =
      async {
        if state.buffer.Length > 0L then
          let len = state.buffer.Length
          return! flushAsync (int(len)) state
        else
          return state
      }

    let writeToBufferAsync (state:MMFPartitionState) (bytes:byte[]) =
      async {
        do! state.buffer.AsyncWrite(bytes)
        return! partialFlushBufferAsync state
      }

    let readFromBufferAsync (state:MMFPartitionState) (count:int) =
      async {
        let take = if int64(count) > state.buffer.Length then int(state.buffer.Length) else count
        let! bytes = state.buffer.AsyncRead(take)
        let! newState = refillBufferAsync state
        return (bytes, newState)
      }