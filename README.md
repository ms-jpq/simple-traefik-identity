# [Simple Traefik Identity](https://ms-jpq.github.io/simple-traefik-identity/)

[![Docker Pulls](https://img.shields.io/docker/pulls/msjpq/simple-traefik-identity.svg)](https://hub.docker.com/r/msjpq/simple-traefik-identity/)

Simple & Configurable -- SSO, for Traefik.

## Preview

### Logon

![login img](https://github.com/ms-jpq/simple-traefik-identity/raw/xp/preview/login.png)

### Logoff

(if not authorized, you can login via another account)

![logoff img](https://github.com/ms-jpq/simple-traefik-identity/raw/xp/preview/logoff.png)

## Features

### Role Based Access Control (RBAC)

```yaml
groups:
  - name: quebec
    sub_domains:
      - "*"
  - name: saskatchewan
    sub_domains:
      - canada.ca
      - www.tourismnewbrunswick.ca
  - name: newfoundland
    sub_domains:
      - www.gov.nu.ca

users:
  - name: yukon
    password: yukon
    session: 0.5 # logs you out after half a day
    groups:
      - quebec
  - name: nunavut
    password: nunavut
    groups:
      - saskatchewan
      - newfoundland
```

### Rate Limit

```yaml
rate_limit:
  headers:
    - Cf-Connecting-Ip
    - Another-Header
    - So-on
  rate: 5
  timer: 30
```

### Custom UI

```yaml
display:
  title: Simple Traefik Identity
  background: |-
    https://github.com/ms-jpq/simple-traefik-identity/raw/xp/src/views/assets/xp.jpg
```

## Usage

See [minimal](https://github.com/ms-jpq/simple-traefik-identity/blob/xp/examples/minimal_conf.yml) and [maximal](https://github.com/ms-jpq/simple-traefik-identity/blob/xp/examples/maximal_conf.yml) to get started.

```yaml
  sti:
    image: msjpq/simple-traefik-identity
    container_name: sti
    labels:
      - traefik.http.services.sti.loadbalancer.server.port=5050
      - traefik.http.middlewares.auth.forwardauth.address=http://sti:5050
      - traefik.http.middlewares.auth.forwardauth.authResponseHeaders=X-Forwarded-User
    volumes:
      - ./config/conf.yml:/sti/config/conf.yml
```

## Security

```txt
👩‍💻 -------- Request --------> 👮‍♀️
👩‍💻 <---- Auth Challenge ----- 👮‍♀️
👩‍💻 ------ Credentials ------> 👮‍♀️
👩‍💻 <-- Samesite JWT Cookie -- 👮‍♀️
```

```txt
👩‍💻 -- Samesite JWT Cookie --> 👮‍♀️
👩‍💻 <---------- OK ----------- 👮‍♀️
👩‍💻 -- Samesite JWT Cookie --> 👮‍♀️
👩‍💻 <---------- OK ----------- 👮‍♀️
```

JWT payload only contain list of accessible domains

## Sister

Check out my sister: [Simple Traefik Dash](https://ms-jpq.github.io/simple-traefik-dash/)

Zero conf service dashboard for Traefik v2
