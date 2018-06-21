#!/bin/bash

# Run the monitoring stack
sudo apt-get update && sudo apt-get install apt-transport-https ca-certificates curl software-properties-common
curl -fsSL https://download.docker.com/linux/ubuntu/gpg | sudo apt-key add -
sudo add-apt-repository "deb [arch=amd64] https://download.docker.com/linux/ubuntu $(lsb_release -cs) stable"
sudo apt-get update && sudo apt-get -y install docker-ce

sudo docker pull sebp/elk
sudo docker pull influxdb
sudo docker pull grafana/grafana
sudo docker pull alpine/socat

sudo docker run -dit -p 5601:5601 -p 9200:9200 -p 5044:5044 -v /var/lib/elasticsearch:/var/lib/elasticsearch --name elk sebp/elk
sudo docker run -dit -p 8086:8086 -v /var/lib/influxdb:/var/lib/influxdb influxdb
sudo docker run -d -v /var/lib/grafana --name grafana-storage busybox:latest
sudo docker run -dit -p 3000:3000 --name=grafana --volumes-from grafana-storage grafana/grafana

# Set up syslog to receive data from haproxy & socat

# Send data to telegraf from syslog

# Send some data to filebeat from syslog

# Run socat to accept requests
sudo docker run -dit --restart unless-stopped -p 5000:5000 alpine/socat -d -d -lmlocal2 -v tcp-l:5000,fork,reuseaddr exec:'/bin/cat'

# Run haproxy with this configuration

# 