ARG BUILDPLATFORM
ARG TARGETPLATFORM

FROM --platform=$BUILDPLATFORM node:22-alpine AS build

WORKDIR /app

COPY package*.json ./
RUN npm ci --ignore-scripts

COPY index.html tsconfig*.json vite.config.ts tailwind.config.js postcss.config.js ./
COPY public public
COPY src src
COPY docker-entrypoint.d docker-entrypoint.d
RUN sed -i 's/\r$//' docker-entrypoint.d/40-runtime-config.sh && chmod +x docker-entrypoint.d/40-runtime-config.sh
RUN npm run build

FROM --platform=$TARGETPLATFORM nginx:alpine
COPY --chown=root:root --chmod=555 --from=build /app/dist /usr/share/nginx/html
COPY --chown=root:root --chmod=444 public/config.template.js /usr/share/nginx/html/config.template.js
COPY --chown=root:root --chmod=555 --from=build /app/docker-entrypoint.d/40-runtime-config.sh /docker-entrypoint.d/40-runtime-config.sh
COPY --chown=root:root --chmod=444 nginx.conf /etc/nginx/conf.d/default.conf
RUN sed -i 's#^[[:space:]]*pid[[:space:]]*/\(var/\)\?run/nginx\.pid;#pid /tmp/nginx.pid;#' /etc/nginx/nginx.conf \
    && grep -q '^[[:space:]]*pid /tmp/nginx.pid;$' /etc/nginx/nginx.conf \
    && mkdir -p /var/cache/nginx /var/run /run \
    && chown -R nginx:nginx /var/cache/nginx /var/run /run
USER nginx
EXPOSE 8080
CMD ["nginx", "-g", "daemon off;"]
