namespace MCDTP.IO.MemoryStream

  open MCDTP.Logging

  // We need to provide an unbounded buffer
  // so that primitive constraints do not effect
  // performance. A list of arrays should help.
  type MemoryStream =
    {
      buffer  : byte[] list
      logger  : ConsoleLogger
    }

  [<RequireQualifiedAccess>]
  [<CompilationRepresentation (CompilationRepresentationFlags.ModuleSuffix)>]
  module MemoryStream =

    let asyncWrite bytes state =
      async {
        try
        let ret =
          if List.isEmpty state.buffer then
            (Array.length bytes), { state with buffer = [bytes] }
          else
            // reverse list to get access to tail element
            let len = Array.length bytes
            let buffer = List.rev state.buffer
            let hBuffer = List.head buffer
            // calculate take in case we are about to exceed
            // size of array
            let take =
              let hBufferLen = Array.length hBuffer
              if (int64 len) + (int64 hBufferLen) > (int64 System.Int32.MaxValue) then
                System.Int32.MaxValue - hBufferLen
              else len
            if take < len then
              // fill current tail, then add new tail with remaining bytes
              let hBuffer = Array.append hBuffer bytes.[..take-1]
              let buffer = hBuffer::(List.tail buffer)
              let hBuffer = bytes.[..(len-take)-1]
              let buffer = hBuffer::buffer
              len, { state with buffer = List.rev buffer }
            else
              // add to tail
              let hBuffer = Array.append hBuffer bytes
              let buffer = hBuffer::(List.tail buffer)
              len, { state with buffer = List.rev buffer }
        state.logger.LogWith(LogLevel.Info,"MemoryStream.asyncWrite",(bytes,ret))
        return ret
        with
        | ex ->
          state.logger.LogWith(LogLevel.Error,"MemoryStream threw exception",ex)
          return -1,state
      }

    let asyncRead take state =
      async {
        try
        // recursively pull from buffer in case
        // we need to remove head
        let ret =
          let rec pull take' buffer =
            match buffer with
            | h::t ->
              let len = Array.length h
              if len < take' then
                let bytes,buffer' = pull (take' - len) t
                (Array.append h bytes),buffer'
              else
                h.[..take'-1], (h.[take'..])::t
            | _ -> [||],buffer
          let bytes,buffer = pull take state.buffer
          (Array.length bytes),bytes,({ state with buffer = buffer })
        state.logger.LogWith(LogLevel.Info,"MemoryStream.asyncRead",(take,ret))
        return ret
        with
        | ex ->
          state.logger.LogWith(LogLevel.Error,"MemoryStream threw exception",ex)
          return -1,[||],state
      }

    let asyncAmend pos bytes state =
      async {
        try
        let nBuffer =
          // recursion should be fine,
          //  asumption is that we won't
          //  be going to deep
          let rec amend pos' bytes' buffers =
            match buffers with
            | x::xs ->
              if Array.isEmpty bytes' then xs
              else
                let len = int64 <| Array.length x
                if pos' + len < pos then amend (pos' + len) bytes' xs
                else
                  let posInX = int <| pos - pos' // guaranteed to be less than max int
                  let byteCount = Array.length bytes'
                  // check if patch is encapsulated in x
                  if posInX + byteCount < int len then
                    bytes'
                    |> Array.iteri(fun i b ->
                      x.[posInX + i] <- b
                    )
                    x::xs
                  else // we need to wrap to next buffer
                    let take = (int len) - posInX
                    bytes'.[..take-1]
                    |> Array.iteri(fun i b ->
                      x.[posInX + i] <- b
                    )
                    // use pos so that pos' - pos = 0 placing
                    //  posInX at beginning of x on next call
                    x::(amend pos bytes'.[take..] xs)
            | _ -> []
          amend pos bytes state.buffer
        let nState = { state with buffer = nBuffer }
        nState.logger.LogWith(LogLevel.Info,"MemoryStream.asyncAmend",(pos,bytes,nState))
        return true,nState
        with
        | ex ->
          state.logger.LogWith(LogLevel.Error,"MemoryStream threw exception",ex)
          return false,state
      }