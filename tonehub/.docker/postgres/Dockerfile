FROM postgres:14-alpine3.15
#COPY hstore.sql /docker-entrypoint-initdb.d
RUN echo "CREATE EXTENSION pg_stat_statements" > /docker-entrypoint-initdb.d/pg_stat_statements.sql