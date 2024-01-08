Start Vue:
1. cd king-of-the-garbage-hill
2. npm run dev

Start Server:
1. cd king-of-the-garbage-hill/server
2. node server.js
3. Test server API: 
```
curl http://localhost:4444/api -H 'Content-Type: application/json' -d '{"text":"sharks"}'
```

Generate Swagger API
npx swagger-typescript-api -p src/services/openapi.yml -o src/services/
