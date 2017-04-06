# Protocol

This outlines version 2 of the protocol for MUDT

Handshake
---------

First off, v1 has never been implemented. It was only used for conceptual purposes. Rather than start off with packet structures, a diagram of messages being sent back and forth will help to visualize the protocol before deconstructing it.

```

                    TCP Handshake
    client                                  server
      |                                       |
      |--connect----------------------------->|
      |<---------------------------specs-req--|
      |--specs-exch-------------------------->|
      |<---------------------------specs-use--|
      |                                       |

```

This is the handshake performed when the client connects to the server. The `connect` is not a packet created by MUDT, it is just connecting using TCP. The `specs-req` is, however, a MUDT generated packet. The server requests to know what specifications the client is operating under. The specifications include the memory limit placed on the application, the degree of parallelization, and which ports are available. This info is sent in the `specs-exch`, which is further broken down in the packet structure section. Based on the specifications the client sends, the server will determine a specification set that is optimal for both client and server. This is the `specs-use` part of the communication. This includes the block size the client should use for hashing, the degree of parallelization, and the ports that the server will be sending to. Note, the degree of parallelization represents the number of concurrent UDP connections. 

```

                  Command Exchange
    client                                  server
      |                                       |
      |--command----------------------------->|
      |<-----------------------------results--|
      |                                       |

```

To interact with the server, the client will support bash like commands. Commands supported will be for traversing directories and selecting files to be transferred. The comand for changing directories will also list all file in the directory. This is to just simplify the interface. The listing will not be very in depth like a the bash command. The only info that will be transferred is the size of a file and its name. There will also be support for getting the path of the working directory.

```

                 Transfer Request
    client                                  server
      |                                       |
      |--transfer-req------------------------>|
      |<-----------------------------prepare--|
      |--ready------------------------------->|
      |                                       |


```

The `transfer-req` is used to signal the server that the client would like a file to be transferred. The request contains the name of the file. The server will respond with a `prepare` message. This client should then create a placeholder file where the transferred file will be written to. The client will send a `ready` message once it has finished preparing for the transfer.

Packet structures
-----------------

The structures between this version and the previous version will not change much. Packets will still be 15 bytes in length. Since `connect` just symbolizes a TCP connection, there is no packet for this. `specs-req` uses the following structure:

```
+----------------------------------+
|  1  |  1  |          13          |
+-----+-----+----------------------+
| 's' | 'r' |         null         |
+-----+-----+----------------------+
```

The client responds with three packets, one for each of the following: parallelization, hash block size, and available ports. The structure for each are:

```
+----------------------------------+
|  1  |  1  |  1  |       12       |
+-----+-----+----------------------+
| 's' | 'e' | prl |      null      |
+-----+-----+----------------------+

+----------------------------------+
|  1  |  1  |      8     |    4    |
+-----+-----+----------------------+
| 's' | 'e' |  hshBlkSz  |   null  |
+-----+-----+----------------------+

+----------------------------------+
|  1  |  1  |    4    |      8     |
+-----+-----+----------------------+
| 's' | 'e' |  nPorts |    null    |
+-----+-----+----------------------+
```

Following the third packet, `nPorts * sizeof(int)` bytes will need to be read. The values are delimited by the fact that the `sizeof(int)` is 4 bytes. So, every 4 bytes is a port. The number of ports will be the same as the level of parallelism specified in the first packet. The server responds with 3 packets:

```
+-----+-----+------------+---------+
|  1  |  1  |      8     |    4    |
+-----+-----+----------------------+
| 's' | 's' |  hshBlkSz  |  null   |
+-----+-----+------------+---------+

+-----+-----+-----+----------------+
|  1  |  1  |  1  |       12       |
+-----+-----+----------------------+
| 's' | 's' | prl |      null      |
+-----+-----+-----+----------------+

+-----+-----+---------+------------+
|  1  |  1  |    4    |      8     |
+-----+-----+----------------------+
| 's' | 's' |  nPorts |    null    |
+-----+-----+---------+------------+
```

The response is very similar to the sent message. The first packet will be the hash block size used for the backlog hashing. The second and third packets are the same as the first and third packet from the message, respectively. Note, the hash block size and parallelization specified by the server, will be <= to the client limit.

------

```
+-----+-------+--------------------+
|  1  |   4   |         10         |
+-----+-------+--------------------+
| 'c' |  cmd  |        null        |
+-----+-------+--------------------+

+-----+-----+---------------+------+
|  1  |  1  |       8       |  5   |
+-----+-----+---------------+------+
| 'c' | 'o' |  payloadSize  | null |
+-----+-----+---------------+------+
```

After the `handshake`, the client can send commands to the server. The first packet above is the client command. The second packet is the response by the server. The response includes an additional `payloadSize` number of bytes after the packet size.

------

```
+-----+-----+----------+-------------------+
|  1  |  1  |     4    |         9         |
+-----+-----+----------+-------------------+
| 't' | 'r' |  fnSize  |        null       |
+-----+-----+----------+-------------------+

+-----+-----+----------------+-------------+
|  1  |  1  |        8       |      5      |
+-----+-----+----------------+-------------+
| 't' | 'p' |    fileSize    |     null    |
+-----+-----+----------------+-------------+

+-----+-----+------------------------------+
|  1  |  1  |             13               |
+-----+-----+------------------------------+
| 't' | 'R' |            null              |
+-----+-----+------------------------------+
```

When the client sends a request to have a file transferred, it informs the server the name of the file. An additional `fnSize` number of bytes will need to be read in on the server side. The server will respond with the size of the file. Once the client has prepared, it will notify the server.