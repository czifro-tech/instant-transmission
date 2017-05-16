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
      startPositionInSource : int64;
      currentPositionInSource : int64;
      positionAtLastEvent : int64;
    }

    static member internal AddChecksum checksums (bytes:byte[]) pos =
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
        incrementalLimit = 0;
        startPositionInSource = 0L;
        currentPositionInSource = 0L;
        positionAtLastEvent = 0L;
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
        incrementalLimit = incrementalLimit;
        startPositionInSource = 0L;
        currentPositionInSource = 0L;
        positionAtLastEvent = 0L;
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
        state' <- { state' with 
                      backlog = Array.append state'.backlog bytes; 
                      currentPositionInSource = state'.currentPositionInSource + int64(len) }
      else
        // clearly we're past the limit. Lets fill remainder of backlog and reset
        //  then we can do solid paging, or, just backlog.
        let diff = state'.backlogLimit - (Array.length state'.backlog)
        let checksumBytes = state'.md5.ComputeHash(Array.append state'.backlog bytes.[..diff-1])
        state' <- { state' with 
                      checksums = HashState.AddChecksum state'.checksums checksumBytes state'.positionAtLastEvent ;
                      backlog = [||];
                      currentPositionInSource = state'.currentPositionInSource + int64(diff);
                      positionAtLastEvent = state'.currentPositionInSource + int64(diff) }
        let numberOfBlocks = float32(len - diff) / float32(state'.backlogLimit) // This should divide evenly, otherwise 0 < numberOfBlocks < 1
        if numberOfBlocks < float32(1.0) then
          state' <- { state' with
                        backlog = bytes.[diff..];
                        currentPositionInSource = state'.currentPositionInSource + int64(len - diff) }
        else
          for i in 0..int(floor(numberOfBlocks))-1 do
            let startPos, endPos = diff + (i*state'.backlogLimit), diff + (i*state'.backlogLimit+(state'.backlogLimit-1))
            let checksumBytes = state'.md5.ComputeHash(bytes.[startPos..endPos])
            state' <- {
                        state' with
                          checksums = HashState.AddChecksum state'.checksums checksumBytes state'.positionAtLastEvent;
                          currentPositionInSource = state'.currentPositionInSource + int64(endPos - startPos);
                          positionAtLastEvent = state'.currentPositionInSource + int64(endPos - startPos)
                      }
      state'
        
    let private doMd5Compute (state:HashState) (bytes:byte[]) =
      if state.isBacklogging then
        backlogAndCompute state bytes
      else
        let checksumBytes = state.md5.ComputeHash(bytes)
        {
          state with
            checksums = HashState.AddChecksum state.checksums checksumBytes state.positionAtLastEvent;
            currentPositionInSource = state.currentPositionInSource + int64(Array.length bytes)
            positionAtLastEvent = state.positionAtLastEvent + int64(Array.length bytes)
        }

    // let private doIHCompute_ (state:HashState) (bytes:byte[]) =
    //   // Fix Issue #16: needed to do blocks iteratively
    //   //  Recursion adds huge finger print in memory
    //   let mutable state' = state
    //   let len = Array.length bytes
    //   // In case state.incrementalCount < state.incrementalLimit
    //   //  a portion, or all, of bytes can fit before having to reset
    //   // If numberOfBlocks is < 1, all of bytes can fit. If a decimal
    //   //  exists and is > 1, 1 or more resets can happen, and a portion
    //   //  will be left
    //   let numberOfBlocks = float(len + state'.incrementalCount) / float(state'.incrementalLimit)

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
                      currentPositionInSource = state'.currentPositionInSource + (int64 len)
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
          let checksumBytes = state'.ih.GetHashAndReset()
          //printfn "Hash Reset Occurred: %s" (state'.ToString())
          state' <- { state' with 
                        checksums = HashState.AddChecksum state'.checksums checksumBytes state'.positionAtLastEvent;
                        currentPositionInSource = state'.currentPositionInSource + int64(endPos - startPos);
                        positionAtLastEvent = state'.currentPositionInSource + int64(endPos - startPos) }

        if overage > 0.0 then 
          let startPos, endPos = blockCount*state'.incrementalLimit, len-1
          state'.ih.AppendData(bytes.[startPos..endPos])
          state' <- { state' with 
                        incrementalCount = endPos - startPos;
                        currentPositionInSource = state'.currentPositionInSource + int64(endPos - startPos);
                        positionAtLastEvent = state'.currentPositionInSource + int64(endPos - startPos) }

      state'

    let computeHash (state:HashState) (bytes:byte[]) = 
      if state.isIncremental then 
        doIHCompute state bytes
      else
        doMd5Compute state bytes

    let private doMd5Finalize (state:HashState) =
      if state.isBacklogging && (Array.length state.backlog) > 0 then
        let checksumBytes = state.md5.ComputeHash(state.backlog)
        let newPosition = state.currentPositionInSource + int64(Array.length checksumBytes)
        let state' =
          {
            state with
              checksums = HashState.AddChecksum state.checksums checksumBytes newPosition;
              currentPositionInSource = newPosition;
              positionAtLastEvent = newPosition
          }
        state',state'.checksums
      else
        state,state.checksums

    let private doIHFinalize (state:HashState) =
      if state.incrementalCount > 0 then
        let checksumBytes = state.ih.GetHashAndReset()
        let newPosition = state.currentPositionInSource + int64(Array.length checksumBytes)
        let state' =
          {
            state with
              checksums = HashState.AddChecksum state.checksums checksumBytes state.positionAtLastEvent;
              currentPositionInSource = newPosition;
              positionAtLastEvent = newPosition
          }
        state',state'.checksums
      else
        state,state.checksums

    let finalizeHash (state:HashState) =
      let finalize = if state.isIncremental then doIHFinalize else doMd5Finalize
      let state,checksum = finalize state
      state,(checksum |> Array.sortBy(fun x -> x.positionInSource))

    let compareHash (hash1:Checksum[]) (hash2:Checksum[]) =
      if Array.length hash1 <> Array.length hash2 then
        printfn "Mismatch lengths..."
        Array.empty
      else
        let checksum =
          Array.map2(fun c1 c2 ->
            let bytesMatch =
              Array.map2 (=) c1.checksum c2.checksum
              |> Array.reduce (&&)
            //printfn "c1: %A,\nc2: %A,\nmatch: %b" c1.checksum c2.checksum bytesMatch
            if not <| bytesMatch then Some c1
            else None
          ) hash1 hash2
          |> Array.filter(fun (co:Checksum option) -> co.IsSome)
        if not <| Array.isEmpty checksum then
          checksum |> Array.map(fun co -> co.Value)
        else
          printfn "Hashes match!"
          Array.empty

    let serialize (checksum:Checksum[]) =
      checksum
      |> Array.collect(fun c ->
        let insertAsBytes (x:System.Object) (offset:int) (bytes':byte[]) =
          x 
          |> MUDT.Utilities.ConversionUtility.getBytes
          |> Array.iteri(fun i b -> bytes'.[i+offset] <- b)
          bytes'
        MUDT.Utilities.TypeUtility.nullByteArray 24 // 8 bytes for c.positionInSource and 16 for c.checksum
        |> insertAsBytes c.positionInSource 0
        |> insertAsBytes c.checksum 8
      )

    let deserialize (bytes:byte[]) =
      let numBlks = (Array.length bytes) / 24
      [|
        for i in 0..numBlks-1 ->
          bytes.[(i*24)..((i*24)+24)-1]
      |]
      |> Array.map(fun block ->
        {
          positionInSource = MUDT.Utilities.ConversionUtility.bytesToInt64 block.[0..7];
          checksum = block.[8..]
        }
      )