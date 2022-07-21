#!/usr/bin/env sh
rm -f Migrations/*.cs var/db/{tonehub.db,tonehub.db-shm,tonehub.db-wal}
dotnet ef migrations add InitialCreate