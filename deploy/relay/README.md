# The relay of shared sessions

`engine/src/Relay/Lapin.Relay` pairs a host (the desktop app sharing its project, File → Share session…) with its guests and moves their frames between them. The frames are encrypted end to end with the invitation's secret, which never reaches the relay; it keeps no project and no state beyond the open sockets (docs/plan-multiplayer.md).

```sh
deploy/relay/deploy.sh     # publish for Alpine, copy to the server, restart the container
```

On the server (`/opt/lapin`), beside `lapin-web` and `lapin-feedback`:

- `docker-compose.yml`:

  ```yaml
    relay:
      image: node:22-alpine          # Alpine with libstdc++; the relay is one self-contained file
      container_name: lapin-relay
      restart: unless-stopped
      command: ["/srv/Lapin.Relay", "--urls", "http://0.0.0.0:8080"]
      volumes:
        - ./relay:/srv:ro
  ```

- `nginx/default.conf`, before `location /`:

  ```nginx
    location /relay/ {
        include /etc/nginx/conf.d/headers.inc;
        resolver 127.0.0.11 valid=30s ipv6=off;
        set $relay http://lapin-relay:8080;
        proxy_pass $relay;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_read_timeout 1h;
        proxy_send_timeout 1h;
        client_max_body_size 0;
    }
  ```

Limits: `LAPIN_RELAY_MAX_ROOMS` (500), `LAPIN_RELAY_MAX_GUESTS` (16 a room), messages up to 256 MB. Anyone may run their own relay the same way; the desktop app shares through it with `LAPIN_RELAY=wss://their.server/relay`, and the invitation's code carries its address.
