namespace MUDT.Cryptography

  open System
  open System.Security.Cryptography
  open MUDT.Diagnostics

  type HashState =
    {
      isIncremental : bool;
      ih : IncrementalHash;
      md5 : MD5;
      checksums : byte[];
      isBacklogging : bool;
      backlog : byte[];
      backlogLimit : int;
      incrementalCount : int;
      incrementalLimit : int;
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

    let private backlogAndCompute (state:HashState) (bytes:byte[]) =
      // recursively compute hash and backlog
      let rec compute (state:HashState) (b:byte[]) =
        let len = Array.length b
        let backlogLen = Array.length state.backlog
        let diff = state.backlogLimit - backlogLen
        if diff > len then // There is enough space to backlog the bytes to hash
          {
            state with
              backlog = b
          }
        else
          if backlogLen = state.backlogLimit then // backlog is full, hash backlog and make rec call
            compute ({
                       state with
                         checksums = Array.append state.checksums (state.md5.ComputeHash(state.backlog))
                     }) b
          else // hash backlog combined with portion of bytes size diff
            compute ({
                       state with
                         checksums = Array.append state.checksums (state.md5.ComputeHash(Array.append state.backlog b.[0..diff-1]))
                         backlog = [||]
                     }) b.[diff..]

      compute state bytes
        
    let private doMd5Compute (state:HashState) (bytes:byte[]) =
      if state.isBacklogging then
        backlogAndCompute state bytes
      else
        {
          state with
            checksums = Array.append state.checksums bytes
        }

    let private doIHCompute (state:HashState) (bytes:byte[]) =
      // recursively update hash
      let rec update (state:HashState) (b:byte[]) =
        if Array.isEmpty(b) then // if b is empty, we don't need to do anything
          state
        elif state.incrementalCount = state.incrementalLimit then // if we have updated {incrementalLimit} number of bytes, finalize hash, store, and reset
          update ({
                    state with
                      checksums = Array.append state.checksums (state.ih.GetHashAndReset());
                      incrementalCount = 0
                  }) b
        else // if hash can be icremented with b do so, otherwise split b
          let len = Array.length b
          let diff = state.incrementalLimit - state.incrementalCount
          let inc, rem =
            if diff > len then
              b, [||]
            else
              b.[0..diff-1], b.[diff..]
          state.ih.AppendData(inc)
          update ({
                    state with
                      incrementalCount = state.incrementalCount + diff
                  }) rem
      update state bytes

    let computeHash (state:HashState) (bytes:byte[]) = 
      if state.isIncremental then 
        doIHCompute state bytes
      else
        doMd5Compute state bytes

    let private doMd5Finalize (state:HashState) =
      if state.isBacklogging && (Array.length state.backlog) > 0 then
        (state.md5.ComputeHash(state.backlog))
        |> Array.append state.checksums
      else
        state.checksums

    let private doIHFinalize (state:HashState) =
      if state.incrementalCount > 0 then
        (state.ih.GetHashAndReset())
        |> Array.append state.checksums
      else
        state.checksums

    let finalizeHash (state:HashState) =
      if state.isIncremental then
        doIHFinalize state
      else
        doMd5Finalize state
