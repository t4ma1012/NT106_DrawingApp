# Kich ban demo playit va realtime

Trang thai: `ACTIVE`

Muc tieu: demo ro duong TCP va UDP theo tung topology, tranh nham rang
LoadBalancer hien tai co UDP relay. Hien tai drawing/chat/login di TCP/TLS;
cursor, laser va UDP ping di UDP/AES khi client ket noi direct toi server.
Neu client di qua LoadBalancer relay, cursor/laser se fallback qua TCP.

## 1. Mot may, khong LoadBalancer

- Chay 1 DrawingServer tren `127.0.0.1:8888` TCP va `127.0.0.1:8889` UDP.
- Chay 2-3 client `Mode Direct`.
- Muc tieu demo: TCP reliable cho draw/flood/text; UDP realtime cho cursor/laser.
- Lenh: xem `role-setup/start-server.ps1` va `role-setup/start-client.ps1`.

## 2. Mot may, co LoadBalancer

- Chay 2 DrawingServer, 1 LoadBalancer, 3 client bang `role-setup/start-local-all.ps1`.
- Client dung `Mode LbRelay`.
- Muc tieu demo: room-affinity, route dung owner server, draw sync realtime qua TCP.
- Luu y: cursor/laser dung TCP fallback vi relay khong proxy UDP.

## 3. LAN, direct TCP+UDP

- Server va client cung LAN.
- Client dung `Mode Direct -Host <server-lan-ip> -TcpPort 8888 -UdpPort 8889`.
- Muc tieu demo: do tre thap nhat, UDP/AES cursor/laser hoat dong dung giao thuc.
- Neu firewall chan UDP, cursor/laser se khong mượt; mo inbound UDP port server.

## 4. LAN, co LoadBalancer

- Server 1/2 chay tren LAN.
- LoadBalancer chay port TCP `9000`, tro `servers.json` den IP LAN cua server.
- Client dung `Mode LbRelay -Host <lb-lan-ip> -TcpPort 9000`.
- Muc tieu demo: 2 backend, room-affinity, client join cung room vao dung owner.
- Luu y: day la TCP relay; cursor/laser fallback TCP.

## 5. Internet playit, direct TCP+UDP

- Tren may server, tao 2 tunnel playit:
  - TCP public -> local `127.0.0.1:8888`.
  - UDP public -> local `127.0.0.1:8889`.
- Client dung `Mode Direct -Host <playit-host> -TcpPort <tcp-public-port> -UdpPort <udp-public-port>`.
- Muc tieu demo: giu dung ca hai giao thuc qua internet.
- Day la kich ban nen dung khi can chung minh UDP realtime hoat dong ngoai LAN.

## 6. Internet playit, co LoadBalancer

- Tren may LB, tao tunnel playit TCP -> local `127.0.0.1:9000`.
- Server backend ket noi voi LB bang LAN/Tailscale/private IP.
- Client dung `Mode LbRelay -Host <playit-host> -TcpPort <tcp-public-port>`.
- Muc tieu demo: public ingress duy nhat, multi-server, room-affinity.
- Luu y: do LB hien tai chi proxy TCP stream, UDP khong di qua LB. Realtime drawing
  van dung TCP reliable; cursor/laser fallback TCP va co the cham hon direct UDP.

## Checklist dong bo realtime

- Tao 1 room tu client A, client B/C join cung room code.
- Ve net lien tuc bang Pen: ben kia phai thay day du, khong mat net.
- Flood fill va text: ben kia thay dung ket qua, client join sau replay dung.
- Cursor/laser:
  - Direct/LAN/playit direct: kiem tra log UDP ping/endpoint va quan sat latency.
  - LB relay: chap nhan TCP fallback, khong dung de ket luan UDP.
- Undo/redo: moi user chi lui/khoi phuc action cua chinh minh.
- Move/resize/delete image/sticker/text/sticky note: ben kia thay dung object, khong tao ban sao.
