# NT106 role setup

This folder is the role-based launcher pack for demos and real deployments.
Each machine only runs the script for its role: client, load balancer, or
server. Run every command from the repository root or from this folder.

## Prerequisites

- Windows 10/11 with PowerShell.
- .NET Framework 4.7.2 Developer Pack or a compatible Visual Studio Build Tools install.
- A filled root `.env` copied from `.env.example`.
- `DrawingServer/server.pfx` and the matching `SERVER_CERT_PASSWORD`.
- Neon/PostgreSQL reachable through `DATABASE_URL`.
- For internet demos, install and sign in to the playit agent. Official playit docs describe the agent as a proxy that supports custom TCP/UDP tunnels.

## Build once

```powershell
dotnet restore .\NT106_DrawingApp.sln /p:RestorePackagesConfig=true
dotnet build .\NT106_DrawingApp.sln -v:minimal
```

## One machine, with LoadBalancer

```powershell
powershell -ExecutionPolicy Bypass -File .\role-setup\start-local-all.ps1 -Build -StopExisting
```

This starts server-1 on TCP/UDP `8888/8889`, server-2 on `8890/8891`,
LoadBalancer on TCP `9000`, and 3 clients in LB relay mode.

## One machine, without LoadBalancer

Shell 1:

```powershell
powershell -ExecutionPolicy Bypass -File .\role-setup\start-server.ps1 -ServerId server-1 -TcpPort 8888 -UdpPort 8889 -Build -StopExisting
```

Shell 2/3/4:

```powershell
powershell -ExecutionPolicy Bypass -File .\role-setup\start-client.ps1 -Mode Direct -Host 127.0.0.1 -TcpPort 8888 -UdpPort 8889 -ClientLabel Client-1
```

Direct mode keeps drawing on TCP and cursor/laser on UDP.

## LAN, with LoadBalancer

Server 1 machine:

```powershell
powershell -ExecutionPolicy Bypass -File .\role-setup\start-server.ps1 -ServerId server-1 -TcpPort 8888 -UdpPort 8889
```

Server 2 machine:

```powershell
powershell -ExecutionPolicy Bypass -File .\role-setup\start-server.ps1 -ServerId server-2 -TcpPort 8890 -UdpPort 8891
```

LoadBalancer machine:

```powershell
powershell -ExecutionPolicy Bypass -File .\role-setup\start-load-balancer.ps1 -ListenPort 9000 -Server1Host <server-1-lan-ip> -Server1TcpPort 8888 -Server1UdpPort 8889 -Server2Host <server-2-lan-ip> -Server2TcpPort 8890 -Server2UdpPort 8891
```

Client machines:

```powershell
powershell -ExecutionPolicy Bypass -File .\role-setup\start-client.ps1 -Mode LbRelay -Host <lb-lan-ip> -TcpPort 9000 -ClientLabel Client-1
```

LB relay mode uses TCP through the LB. It preserves reliable drawing sync and
uses TCP fallback for cursor/laser because the current LB does not relay UDP.

## LAN, direct TCP+UDP

Use this when you want to verify the UDP realtime path explicitly:

```powershell
powershell -ExecutionPolicy Bypass -File .\role-setup\start-client.ps1 -Mode Direct -Host <server-lan-ip> -TcpPort 8888 -UdpPort 8889 -ClientLabel Client-1
```

## Internet with playit, direct TCP+UDP

On the server machine, start server-1 locally:

```powershell
powershell -ExecutionPolicy Bypass -File .\role-setup\start-server.ps1 -ServerId server-1 -TcpPort 8888 -UdpPort 8889
```

In playit, create tunnels that point to the same local server:

- TCP tunnel: local address `127.0.0.1:8888`.
- UDP tunnel: local address `127.0.0.1:8889`.

On every internet client, use the public host/ports shown by playit:

```powershell
powershell -ExecutionPolicy Bypass -File .\role-setup\start-client.ps1 -Mode Direct -Host <playit-host> -TcpPort <playit-tcp-port> -UdpPort <playit-udp-port> -ClientLabel Client-1
```

This is the internet scenario that preserves both protocols end to end:
drawing/chat/login use TCP/TLS; cursor/laser/UDP ping use UDP/AES.

## Internet with playit, LoadBalancer relay

On backend server machines, start the servers normally. On the LB machine,
create a playit TCP tunnel to local `127.0.0.1:9000`, then run:

```powershell
powershell -ExecutionPolicy Bypass -File .\role-setup\start-load-balancer.ps1 -ListenPort 9000 -Server1Host <server-1-private-ip> -Server1TcpPort 8888 -Server1UdpPort 8889 -Server2Host <server-2-private-ip> -Server2TcpPort 8890 -Server2UdpPort 8891 -StartPlayitAgent
```

Clients connect through the playit TCP endpoint:

```powershell
powershell -ExecutionPolicy Bypass -File .\role-setup\start-client.ps1 -Mode LbRelay -Host <playit-host> -TcpPort <playit-tcp-port> -ClientLabel Client-1
```

This scenario is best for multi-server room-affinity demos. Realtime drawing is
still reliable because draw/fill/text go over TCP. Cursor/laser use TCP fallback
through the relay; the current LoadBalancer is not a UDP proxy.
