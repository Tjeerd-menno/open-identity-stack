FROM node:22-alpine AS build

WORKDIR /workspace

COPY src/frontend-packages/admin-api-client ./src/frontend-packages/admin-api-client
COPY src/OpenIdentityStack.ManagementWeb/package.json ./src/OpenIdentityStack.ManagementWeb/package.json
COPY src/OpenIdentityStack.ManagementWeb/package-lock.json ./src/OpenIdentityStack.ManagementWeb/package-lock.json

WORKDIR /workspace/src/OpenIdentityStack.ManagementWeb
RUN npm ci --ignore-scripts

COPY src/OpenIdentityStack.ManagementWeb ./
RUN npm run build

FROM nginx:1.29-alpine

COPY src/OpenIdentityStack.ManagementWeb/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /workspace/src/OpenIdentityStack.ManagementWeb/dist /usr/share/nginx/html

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
