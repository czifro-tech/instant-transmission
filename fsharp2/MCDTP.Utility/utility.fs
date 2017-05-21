namespace MCDTP.Utility

  module Conversion =

    let bytesToInt16 (bytes:byte[]) =
      System.BitConverter.ToInt16(bytes,0)

    let bytesToInt (bytes:byte[]) =
      System.BitConverter.ToInt32(bytes,0)

    let bytesToInt64 (bytes:byte[]) =
      System.BitConverter.ToInt64(bytes,0)
    
    let bytesToUInt16 (bytes:byte[]) =
      System.BitConverter.ToUInt16(bytes,0)

    let bytesToUInt32 (bytes:byte[]) =
      System.BitConverter.ToUInt32(bytes,0)

    let bytesToUTF8 (bytes:byte[]) =
      System.Text.Encoding.UTF8.GetString(bytes)

    let utf8ToBytes (str:string) =
      System.Text.Encoding.UTF8.GetBytes(str)

    let int16ToBytes (v:int16) =
      System.BitConverter.GetBytes(v)

    let intToBytes (v:int) =
      System.BitConverter.GetBytes(v)

    let int64ToBytes (v:int64) =
      System.BitConverter.GetBytes(v)

    let uint16ToBytes (v:uint16) =
      System.BitConverter.GetBytes(v)

    let uint32ToBytes (v:uint32) =
      System.BitConverter.GetBytes(v)

    let getBytes (x:System.Object) =
      match x with
      | :? int16 as ix -> int16ToBytes(ix)
      | :? int as ix -> intToBytes(ix)
      | :? int64 as ix -> int64ToBytes(ix)
      | :? uint16 as ux -> uint16ToBytes(ux)
      | :? uint32 as ux -> uint32ToBytes(ux)
      | :? string as sx -> utf8ToBytes(sx)
      | :? byte as b -> [| b |]
      | :? array<byte> as bytes -> bytes
      | _ -> failwithf "Error: unsupported type '%s'" ((x.GetType()).ToString())

  module Type =

    let nullByte = byte 0uy

    let nullByteArray size =
      [| for i in 0 .. size-1 -> nullByte |]

  module Sync =

    open System.Threading

    let createLock() = new ReaderWriterLockSlim()

    let write (func:unit->unit) (locker:ReaderWriterLockSlim) =
      locker.EnterWriteLock()
      try
        func ()
      finally
        locker.ExitWriteLock()

    let read (func:unit->'a) (locker:ReaderWriterLockSlim) =
      locker.EnterReadLock()
      try
        func ()
      finally
        locker.ExitReadLock()