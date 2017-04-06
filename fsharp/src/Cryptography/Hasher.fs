namespace MUDT.Cryptography

  open System.Security.Cryptography

  type Checksum =
    {
      checksum : byte[];
      positionInSource : int64;
    }

  type HashState =
    {
      isIncremental : bool;
      ih : IncrementalHash;
      md5 : MD5;
      checksums : Checksum[];
      isBacklogging : bool;
      backlog : byte[];
      backlogLimit : int;
      incrementalCount : int;
      incrementalLimit : int;
    }

    static member internal AddChecksum checksums (bytes:byte[]) =
      let pos =
        if Array.isEmpty checksums then
          0L
        else
          (Array.last checksums).positionInSource + int64(Array.length bytes)
      let checksum = { checksum = bytes; positionInSource = pos }
      Array.append checksums [| checksum |]

  type HashStateConfig =
    {
      doIncremental : bool;
      doBacklogging : bool;
      useBacklogLimit : int;
      useIncrementalLimit : int;
    }

    static member internal DefaultInstance () =
      {
        doIncremental = false;
        doBacklogging = false;
        useBacklogLimit = 0;
        useIncrementalLimit = 0;
      }

    static member IHConfig (incrementalLimit:int) =
      {
        HashStateConfig.DefaultInstance() with
          doIncremental = true;
          useIncrementalLimit = incrementalLimit;
      }

    static member Md5Config () =
      HashStateConfig.DefaultInstance()

    static member BackloggingMd5Config (backlogLimit:int) =
      {
        HashStateConfig.DefaultInstance() with
          doBacklogging = true;
          useBacklogLimit = backlogLimit;
      }
  
  module Hasher =

    let createMd5HashState (isBacklogging:bool) (backlogLimit:int) =
      {
        isIncremental = false;
        ih = null;
        md5 = MD5.Create();
        checksums = [||];
        isBacklogging = isBacklogging
        backlog = [||];
        backlogLimit = backlogLimit;
        incrementalCount = 0;
        incrementalLimit = 0
      }

    let createIHHashState (incrementalLimit:int) =
      {
        isIncremental = true;
        ih = IncrementalHash.CreateHash(HashAlgorithmName.MD5);
        md5 = null;
        checksums = [||];
        isBacklogging = false; // Since we're incrementing, there is no need to backlog any unhashed bytes
        backlog = [||];
        backlogLimit = 0;
        incrementalCount = 0;
        incrementalLimit = incrementalLimit
      }

    let createHashState (config:HashStateConfig) =
      if (config.doIncremental) then
        createIHHashState config.useIncrementalLimit
      else
        createMd5HashState config.doBacklogging config.useBacklogLimit

    let private backlogAndCompute (state:HashState) (bytes:byte[]) =
      // Fix Issue #16: needed to do blocks iteratively
      //  Recursion adds huge finger print in memory
      let mutable state' = state
      let len = Array.length bytes
      if (Array.length state'.backlog) + len < state'.backlogLimit then
        state' <- { state' with backlog = Array.append state'.backlog bytes }
      else
        // clearly we're at a past the limit. Lets fill remainder of backlog and reset
        //  then we can do solid paging, or, just backlog.
        let diff = state'.backlogLimit - (Array.length state'.backlog)
        state' <- { state' with 
                      checksums = HashState.AddChecksum state'.checksums (state'.md5.ComputeHash(Array.append state'.backlog bytes.[..diff-1])) ;
                      backlog = [||] }
        let numberOfBlocks = float32(len - diff) / float32(state'.backlogLimit) // This should divide evenly, otherwise 0 < numberOfBlocks < 1
        if numberOfBlocks < float32(1.0) then
          state' <- { state' with backlog = bytes.[diff..] }
        else
          for i in 0..int(floor(numberOfBlocks))-1 do
            let startPos, endPos = diff + (i*state'.backlogLimit), diff + (i*state'.backlogLimit+(state'.backlogLimit-1))
            state' <- {
                        state' with
                          checksums = HashState.AddChecksum state'.checksums (state'.md5.ComputeHash(bytes.[startPos..endPos]))
                      }
      state'
        
    let private doMd5Compute (state:HashState) (bytes:byte[]) =
      if state.isBacklogging then
        backlogAndCompute state bytes
      else
        {
          state with
            checksums = HashState.AddChecksum state.checksums (state.md5.ComputeHash(bytes))
        }

    let private doIHCompute (state:HashState) (bytes:byte[]) =
      // Fix Issue #16: needed to do blocks iteratively
      //  Recursion adds huge finger print in memory
      let mutable state' = state
      let len = Array.length bytes
      // In case state.incrementalCount < state.incrementalLimit
      //  a portion, or all, of bytes can fit before having to reset
      // If numberOfBlocks is < 1, all of bytes can fit. If a decimal
      //  exists and is > 1, 1 or more resets can happen, and a portion
      //  will be left
      let numberOfBlocks = float(len + state'.incrementalCount) / float(state'.incrementalLimit)
      if numberOfBlocks < 1.0 then
        state' <- {
                    state' with
                      incrementalCount = state'.incrementalCount + len
                  }
        state'.ih.AppendData(bytes)
      else
        let blockCount = int(floor(numberOfBlocks))
        let overage = float(numberOfBlocks - float(blockCount))

        for i in 0..blockCount-1 do
          let mutable startPos, endPos = i*state'.incrementalLimit, i*state'.incrementalLimit+(state'.incrementalLimit-1)
          if state'.incrementalCount > 0 then 
            endPos <- startPos + (state'.incrementalLimit - state'.incrementalCount)
            state' <- { state' with incrementalCount = 0 }
          state'.ih.AppendData(bytes.[startPos..endPos])
          state' <- { state' with checksums = HashState.AddChecksum state'.checksums (state'.ih.GetHashAndReset()) }

        if overage > 0.0 then 
          let startPos, endPos = blockCount*state'.incrementalLimit, len-1
          state'.ih.AppendData(bytes.[startPos..endPos])
          state' <- { state' with incrementalCount = endPos - startPos }

      state'

    let computeHash (state:HashState) (bytes:byte[]) = 
      if state.isIncremental then 
        doIHCompute state bytes
      else
        doMd5Compute state bytes

    let private doMd5Finalize (state:HashState) =
      if state.isBacklogging && (Array.length state.backlog) > 0 then
        HashState.AddChecksum state.checksums (state.md5.ComputeHash(state.backlog))
      else
        state.checksums

    let private doIHFinalize (state:HashState) =
      if state.incrementalCount > 0 then
        HashState.AddChecksum state.checksums (state.ih.GetHashAndReset())
      else
        state.checksums

    let finalizeHash (state:HashState) =
      (if state.isIncremental then
        doIHFinalize state
      else
        doMd5Finalize state)
      |> Array.sortBy(fun x -> x.positionInSource)
      |> Array.map(fun x -> x.checksum)
      |> Array.concat
