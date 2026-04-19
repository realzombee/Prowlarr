# syntax=docker/dockerfile:1
ARG BASE_IMAGE=ghcr.io/linuxserver/prowlarr:develop
FROM ${BASE_IMAGE}

ARG BUILD_DATE
ARG VERSION
ARG PROWLARR_BRANCH=develop
ARG PACKAGE_AUTHOR="github.com/realzombee/Prowlarr"
ARG PROWLARR_REPO="realzombee/Prowlarr"
ARG TARGETARCH

LABEL org.opencontainers.image.created="${BUILD_DATE}" \
  org.opencontainers.image.source="${PACKAGE_AUTHOR}" \
  org.opencontainers.image.version="${VERSION}"

RUN apk add --no-cache curl tar && \
  mkdir -p /app/prowlarr/bin /tmp/prowlarr && \
  case "$TARGETARCH" in \
    amd64) runtime="linux-musl-core-x64" ;; \
    arm64) runtime="linux-musl-core-arm64" ;; \
    *) echo "Unsupported TARGETARCH: $TARGETARCH" >&2; exit 1 ;; \
  esac && \
  curl -fsSL -o /tmp/prowlarr/prowlarr.tar.gz \
    "https://github.com/${PROWLARR_REPO}/releases/download/v${VERSION}/Prowlarr.${PROWLARR_BRANCH}.${VERSION}.${runtime}.tar.gz" && \
  rm -rf /app/prowlarr/bin/* && \
  tar -xzf /tmp/prowlarr/prowlarr.tar.gz -C /app/prowlarr/bin --strip-components=1 && \
  echo -e "UpdateMethod=docker\nBranch=${PROWLARR_BRANCH}\nPackageVersion=${VERSION:-LocalBuild}\nPackageAuthor=${PACKAGE_AUTHOR}" > /app/prowlarr/package_info && \
  printf "Linuxserver.io version: ${VERSION}\nBuild-date: ${BUILD_DATE}" > /build_version && \
  echo "**** cleanup ****" && \
  rm -rf \
    /app/prowlarr/bin/prowlarr.Update \
    /tmp/*
