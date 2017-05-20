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
              let hBuffer = Array.append hBuffer (Array.take take bytes)
              let buffer = hBuffer::(List.tail buffer)
              let hBuffer = Array.take (len - take) bytes
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
                let bytes,buffer = pull (take' - len) t
                (Array.append h bytes),buffer
              else
                (Array.take take' h), (Array.skip take' h)::t
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