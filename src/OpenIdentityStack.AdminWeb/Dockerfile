ARG BUILDPLATFORM
ARG TARGETPLATFORM

FROM --platform=$BUILDPLATFORM node:22-alpine AS build

WORKDIR /app

COPY package*.json ./
RUN npm ci --ignore-scripts

COPY . .
RUN sed -i 's/\r$//' docker-entrypoint.d/40-runtime-config.sh && chmod +x docker-entrypoint.d/40-runtime-config.sh
RUN npm run build

FROM --platform=$TARGETPLATFORM nginx:alpine
COPY --chown=nginx:nginx --from=build /app/dist /usr/share/nginx/html
COPY --chown=nginx:nginx public/config.template.js /usr/share/nginx/html/config.template.js
COPY --chown=nginx:nginx public/config.template.js /usr/share/nginx/html/config.js
COPY --chown=nginx:nginx --from=build /app/docker-entrypoint.d/40-runtime-config.sh /docker-entrypoint.d/40-runtime-config.sh
COPY nginx.conf /etc/nginx/conf.d/default.conf
RUN mkdir -p /var/cache/nginx /var/run \
    && chown -R nginx:nginx /usr/share/nginx/html /docker-entrypoint.d /var/cache/nginx /var/run
USER nginx
EXPOSE 8080
CMD ["nginx", "-g", "daemon off;"]
