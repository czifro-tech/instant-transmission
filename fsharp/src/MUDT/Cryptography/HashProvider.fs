namespace MUDT.Cryptography

  open System

  [<AbstractClass>]
  [<AllowNullLiteral>]
  type HashProvider() =
    member x.AppendHashData(data:byte[], offset:int, count:int) =
      if isNull(data) then raise (ArgumentNullException("data"))
      if offset < 0 then raise (ArgumentOutOfRangeException("offset"))
      if count < 0 then raise (ArgumentOutOfRangeException("count"))
      if ((Array.length data) - offset) < count then
        raise (ArgumentException("Invalid Offset"))
      x.AppendHashDataCore(data, offset, count)

    abstract member AppendHashDataCore : data:byte[] * offset:int * count:int -> unit 

    abstract member FinalizeHashAndReset : unit -> byte[]

    abstract member HashSizeInBytes : int with get

    abstract member Dispose : disposing:bool -> unit

    interface IDisposable with
      member x.Dispose() =
        x.Dispose(true)
        GC.SuppressFinalize(x)