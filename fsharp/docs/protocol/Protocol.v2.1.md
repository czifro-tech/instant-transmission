# Protocol

This outlines the revisions of the version 2 of the protocol of MUDT

Handshake
---------

Command Exchange has been removed for now so that the core protocol can be finished.

Transmission/Retransmission
---------------------------

As outlined in the previous doc, transmission begins after the client notifies the server that the client is ready to receive data. Packet structure of file data is as outlined in v1. The part that has changed is the retransmission bit.
