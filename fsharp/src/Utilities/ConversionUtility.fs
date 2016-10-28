namespace MUDT.Utilities

  module ConversionUtility =

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

    let intToBytes (v:int) =
      System.BitConverter.GetBytes(v)

    let uint16ToBytes (v:uint16) =
      System.BitConverter.GetBytes(v)

    let uint32ToBytes (v:uint32) =
      System.BitConverter.GetBytes(v)

    let getBytes (x:System.Object) =
      match x with
      | :? int as ix -> intToBytes(ix)
      | :? uint16 as ux -> uint16ToBytes(ux)
      | :? uint32 as ux -> uint32ToBytes(ux)
      | :? string as sx -> utf8ToBytes(sx)
      | :? byte as b -> [| b |]
      | :? array<byte> as bytes -> bytes
      | _ -> failwithf "Error: unsupported type"