#!/usr/bin/env sh
docker-compose -f docker-compose-dbonly.yml stop
rm -f Migrations/*.cs var/db/{tonehub.db,tonehub.db-shm,tonehub.db-wal}
sudo rm -r ../var/docker/
docker-compose -f docker-compose-dbonly.yml up --detach
dotnet ef migrations add InitialCreate
