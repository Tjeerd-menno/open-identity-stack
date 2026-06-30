FROM node:22-alpine AS build

WORKDIR /workspace/src/OpenIdentityStack.ManagementWeb.Legacy

COPY src/OpenIdentityStack.ManagementWeb.Legacy/package.json ./package.json
COPY src/OpenIdentityStack.ManagementWeb.Legacy/package-lock.json ./package-lock.json

RUN npm ci --ignore-scripts

COPY src/OpenIdentityStack.ManagementWeb.Legacy ./
RUN npm run build

FROM nginx:1.29-alpine

COPY src/OpenIdentityStack.ManagementWeb.Legacy/nginx.conf /etc/nginx/conf.d/default.conf
COPY src/OpenIdentityStack.ManagementWeb.Legacy/docker-entrypoint.sh /docker-entrypoint.d/40-openidentitystack-runtime-config.sh
COPY --from=build /workspace/src/OpenIdentityStack.ManagementWeb.Legacy/dist /usr/share/nginx/html
RUN chmod +x /docker-entrypoint.d/40-openidentitystack-runtime-config.sh

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
