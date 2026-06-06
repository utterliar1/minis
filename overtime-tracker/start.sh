#!/bin/sh
# Start Gunicorn in background
gunicorn -w 2 -b 127.0.0.1:5000 app:app --daemon --access-logfile /dev/null --error-logfile /dev/null
# Start Nginx in foreground
nginx -g 'daemon off;'
