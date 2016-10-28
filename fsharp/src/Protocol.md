# Protocol

This outlines the protocol for MUDT

Packet Structures
-----------------

Packets sent across the TCP connection start with a single byte that acts as a switch so that MUDT knows how to process it. All TCP packets will be read in as 15 byte blocks. This does not refer to reading in packets at the Transport layer. A packet in the MUDT protocol means a chunk of data read from a TCP stream or UDP stream. The following are the structures used by MUDT:

- File Meta Data Header (META):
   ```
    -----------------------------------
   |  1  |     8      |   4   |   2    |
    -----------------------------------
   | 'm' |  fileSize  | fnLen |  NULL  |
    -----------------------------------
   ```
   This header is followed by the name of the file. `fnLen` is the length of the file name
- List of ports (PORTS):
   ```
    ---------------------------
   |  1  |      4     |   10   |
    ---------------------------
   | 'p' |  numPorts  |  NULL  |
    ---------------------------
   ```
   This header is followed by a list of ports. Ports are not delimited. One port is 4 bytes, the size of an integer.
- Action (ACT):
   ```
    ----------------
   |  1  |    1     |
    ----------------
   | 'a' |  action  |
    ----------------
   ```
   `action` == 0 -> initiate transfer, `action` == 1 -> abort
- File data (SEG):
   ```
    ----------------------------
   |    8     |   4    |  500   |
    ----------------------------
   |  seqNum  |  dLen  |  data  |
    ----------------------------
   ```
   This packet is transmitted over UDP. The packet will be 512 bytes in size. `seqNum` is the data chunk's position in the file.
- Digest exchange (CSUM):
   ```
    --------------------------------
   |  1  |   4    |   4    |   6    |
    --------------------------------
   | 'd' |  pNum  |  dLen  |  NULL  |
    --------------------------------
   ```
   The ‘pNum’ field is to identify which partition the incoming digest belongs to. The port number the UDP connection will be used for ‘pNum’. The ‘dLen’ field is the length of the digest.
- Digest validation (CSVAL):
   ```
    ----------------------------------
   |  1  |    1     |   4    |   9    |
    ----------------------------------
   | 'v' |  result  |  pNum  |  NULL  |
    ----------------------------------
   ```
   If the ‘result’ field is 0, the digest was valid for the partition specified by ‘pNum’. If ‘result’ is 1, the digest was invalid. This will trigger both parties to prepare for retransmission.
- Packet dropped (PDROP):
   ```
    -------------------------
   |  1  |    8     |   6    |
    -------------------------
   | 'x' |  seqNum  |  NULL  |
    -------------------------
   ```
- Ping/Pong (PING):
   ```
    ----------------------------
   |  1  |      8      |   6    |
    ----------------------------
   | 'P' |  timestamp  |  NULL  |
    ----------------------------
   ```
   The ‘timestamp’ field will be a UTC stamp as a long.

Handshake
---------
The handshake step happens over TCP. The sender starts with a META packet. The receiver will allocate a padded file of specified size. While the receiver creates a file, the sender and receiver negotiate the number of connections to use. The sender and receiver will push to use as many connections as possible. However, there are a limited number of ports. Being greedy and using all available ports could interfere with other processes. The sender and receiver will have composed a list of available ports and exchange lists as a PORTS packet. The sender and receiver will perform intersects on exchanged lists and their own lists. The availability of these ports will be validated. Those that are unavailable will be removed and then another list exchange will happen. This process will continue until both parties exchange matching lists. After the negotiation, the sender will wait for the receiver to notify it is ready to receive, an ACT packet. Once the sender and receiver are ready, data transmission begins.

Transmission
------------
The sender will partition the file by the number of opened connections. This done by having multiple file pointers point to the beginning of each partition. Buffers are used by both parties to hold portions of each partition. To ensure a continuous flow of data, buffers on the sender’s side will replenish once halfway depleted while buffers on the receiver’s side will flush once half filled. A SEG packet is used to transmit a portion of a partition. Once the transfer has finished, the calculated digest checksum for each partition from the receiver’s side will be sent to the sender as a CSUM packet. The sender will respond with a CSVAL packet. If a partition’s checksum does not match, the sender will tell the receiver to prepare for retransmission of whichever partitions did not match. This method prevents the need to resend the entire file. If only a portion of the file came through corrupted, then only that portion should be resent. The receiver will notify the sender it is ready with an ACT packet.

Reliability
-----------
Packet loss and congestion are inevitable with networking. MUDT notifies packet loss over the TCP connection, similar to Tsunami, using a PDROP packet. However, all missing packets are retransmitted at the end. This is so that flow is not interrupted. Congestion will be tracked through the RTT of PING packets. As the RTT increases, the throughput will be throttled to decrease the frequency of packet transmission.
