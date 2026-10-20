# Byggkontekst er reporoten, ikke denne mappa:
#   podman build -f Artskart3.WebApp/Dockerfile -t artskart3-web .
#
# Denne containeren er eneste offentlige inngang. Den serverer Angular-bygget og
# videresender API-stiene til API-containeren, slik at frontend og API deler
# vertsnavn. Se nginx/default.conf.template for hvorfor det må være slik.

FROM node:24-bookworm-slim AS build
WORKDIR /src/Artskart3.WebApp
COPY Artskart3.WebApp/package.json Artskart3.WebApp/package-lock.json ./
RUN npm ci
COPY Artskart3.WebApp/ ./
RUN npm run build -- --configuration production

# Forkomprimerer det komprimerbare, slik at nginx kan servere ferdige .gz-filer med
# gzip_static i stedet for å komprimere på nytt for hver forespørsel. Angular
# lager ikke disse selv; de .br- og .gz-filene som lå i det gamle API-imaget kom
# fra ASP.NET sin static-assets-pipeline.
#
# Brotli er utelatt med vilje: modulen må bygges mot samme nginx-versjon som
# imaget, og den koblingen knekker ved neste oppdatering. Prisen er en knapp
# tidel større førstelast.
RUN find dist/artskart3.webapp/browser \
      -type f \( -name '*.js' -o -name '*.css' -o -name '*.html' -o -name '*.svg' \
                 -o -name '*.json' -o -name '*.txt' \) \
      -exec gzip -9 -k {} \;

FROM nginx:stable-alpine AS final

# Bare disse variablene settes inn i konfigurasjonen. Uten filteret ville envsubst
# også kunne rote med nginx sine egne variabler.
ENV NGINX_ENVSUBST_FILTER='API_|NGINX_LOCAL_RESOLVERS'
ENV API_ORIGIN=http://api:8080

# Får imaget sitt entrypoint til å lese nameserverne fra /etc/resolv.conf og
# eksportere dem som NGINX_LOCAL_RESOLVERS. Uten dette kan ikke konfigurasjonen
# sette opp en resolver, og da slår nginx opp API-et EN gang ved oppstart og
# cacher adressen for alltid - med 502 til følge neste gang API-containeren
# byttes ut. Se kommentaren ved proxy_pass.
ENV NGINX_ENTRYPOINT_LOCAL_RESOLVERS=1

# TLS er av som standard. I Azure terminerer gateway/ingress TLS; lokalt slås det
# på i docker-compose.yml fordi identitetsleverandøren krever https-returadresser.
ENV TLS_ENABLED=0

# openssl trengs bare for å lage et selvsignert sertifikat til lokal testing.
RUN apk add --no-cache openssl

COPY Artskart3.WebApp/nginx/default.conf.template /etc/nginx/templates/default.conf.template
COPY Artskart3.WebApp/nginx/25-tls.sh /docker-entrypoint.d/25-tls.sh
RUN chmod +x /docker-entrypoint.d/25-tls.sh
COPY --from=build /src/Artskart3.WebApp/dist/artskart3.webapp/browser/ /usr/share/nginx/html/

EXPOSE 8080 8443
