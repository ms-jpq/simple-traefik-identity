# [Simple Traefik Identity](https://ms-jpq.github.io/simple-traefik-identity/)

[![Docker Pulls](https://img.shields.io/docker/pulls/msjpq/simple-traefik-identity.svg)](https://hub.docker.com/r/msjpq/simple-traefik-identity.svg/)

Simple & Configurable -- SSO, for Traefik.


## Preview

### Logon

![login img](https://github.com/ms-jpq/simple-traefik-identity/raw/master/preview/login.png)

### Logoff

(if not authorized, you can login via another account)

![logoff img](https://github.com/ms-jpq/simple-traefik-identity/raw/master/preview/logoff.png)

## Usage

## Customization

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
