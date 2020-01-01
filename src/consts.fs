namespace STI

open System
open System.IO

module Consts =

    let PROJECTURI = "https://ms-jpq.github.io/simple-traefik-identity/"

    let CONTENTROOT = Directory.GetCurrentDirectory()

    let CONFFILE = Path.Combine(CONTENTROOT, "config", "conf.yml")

    let RESOURCESDIR = Path.Combine(CONTENTROOT, "views/")

    let BACKGROUND = "/assets/xp.jpg"

    let ENVPREFIX = "STI"

    let private readme =
        sprintf """
Simple Traefik Identity (STI)
STI is a Single Sign-On service for Traefik
==============================================================================
For usage, please reference
https://ms-jpq.github.io/simple-traefik-identity/
"""

    let README = readme

    let WEBSRVPORT = 5050

    let DEFAULTTITLE = "Simple Traefik Identity"

    let COOKIENAME = "_sti_jwt"

    let COOKIEMAXAGE = TimeSpan.FromDays(4200.0)

    let TOKENISSUER = "STI"

    let TOKENAUDIENCE = "STI"

    let TOKENLIFESPAN = TimeSpan.FromHours(24.0)

    let REMOTEADDR = "X-Forwarded-For"

    let RATELIMIT = TimeSpan.FromSeconds(5.0)

    let RATE = 5
