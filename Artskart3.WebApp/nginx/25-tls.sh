#!/bin/sh
# Bestemmer hvilke porter nginx lytter på, og setter opp TLS når det er slått på.
#
# Kjøres av imagets entrypoint før nginx starter. Skriver små include-filer med
# listen-direktiver, slik at server-blokka i default.conf bare finnes i EN utgave -
# ellers måtte hele stilista vedlikeholdes to steder.
#
# TLS er AV som standard. I Azure terminerer gateway/ingress TLS, og containeren skal
# ikke gjøre det samme arbeidet en gang til. Lokalt slås det på fordi
# identitetsleverandøren krever https-returadresser.
#
# Sertifikat: settes TLS_CERT_FILE og TLS_KEY_FILE, brukes de. Ellers lages et
# selvsignert for lokal testing. Ingen nøkkel bakes inn i imaget.

set -eu

LISTEN_DIR=/etc/nginx/listen
SELF_DIR=/etc/nginx/certs-selfsigned
CERT="${TLS_CERT_FILE:-$SELF_DIR/tls.crt}"
KEY="${TLS_KEY_FILE:-$SELF_DIR/tls.key}"

mkdir -p "$LISTEN_DIR"
printf 'listen %s;\n' "${HTTP_PORT:-8080}" > "$LISTEN_DIR/00-http.conf"
rm -f "$LISTEN_DIR/10-tls.conf"

if [ "${TLS_ENABLED:-0}" != "1" ]; then
    echo "TLS: av (TLS_ENABLED=${TLS_ENABLED:-0}), lytter bare på ${HTTP_PORT:-8080}"
    exit 0
fi

if [ -s "$CERT" ] && [ -s "$KEY" ]; then
    echo "TLS: bruker sertifikat $CERT"
elif [ -n "${TLS_CERT_FILE:-}${TLS_KEY_FILE:-}" ]; then
    # Er et sertifikat eksplisitt konfigurert, men mangler, skal det smelle. Aa falle
    # tilbake til selvsignert her ville skjult en feil i monteringen.
    echo "TLS: konfigurert sertifikat mangler ($CERT / $KEY)" >&2
    exit 1
else
    echo "TLS: ingen sertifikat konfigurert, lager selvsignert for lokal testing"
    mkdir -p "$SELF_DIR"
    openssl req -x509 -newkey rsa:2048 -sha256 -days 365 -nodes \
        -keyout "$KEY" -out "$CERT" \
        -subj "/CN=${TLS_COMMON_NAME:-localhost}" \
        -addext "subjectAltName=DNS:${TLS_COMMON_NAME:-localhost},DNS:localhost,IP:127.0.0.1" \
        >/dev/null 2>&1
    chmod 600 "$KEY"
fi

cat > "$LISTEN_DIR/10-tls.conf" <<EOF
listen ${TLS_PORT:-8443} ssl;
http2 on;
ssl_certificate     $CERT;
ssl_certificate_key $KEY;
ssl_protocols       TLSv1.2 TLSv1.3;
ssl_session_cache   shared:SSL:1m;
ssl_session_timeout 10m;
EOF

echo "TLS: på, lytter på ${TLS_PORT:-8443}"
