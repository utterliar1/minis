FROM python:3.12-alpine

RUN apk add --no-cache nginx

WORKDIR /app
COPY backend/requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY backend/app.py /app/app.py
COPY frontend/ /app/frontend/
COPY nginx.conf /etc/nginx/nginx.conf
COPY start.sh /app/start.sh
RUN sed -i 's/\r$//' /app/start.sh && chmod +x /app/start.sh

RUN mkdir -p /data /run/nginx

EXPOSE 80
CMD ["/app/start.sh"]
