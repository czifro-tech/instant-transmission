namespace MUDT.Utilities

  module TypeUtility =

    let nullByte = byte 0uy

    let nullByteArray size =
      [| for i in 0 .. size -> nullByte |]