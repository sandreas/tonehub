#!/usr/bin/env sh
UID=${UID} GID=${GID} docker-compose -f docker-compose-dbonly.yml up
