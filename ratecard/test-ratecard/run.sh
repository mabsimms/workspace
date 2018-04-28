#!/bin/bash

mvn exec:java -Dexec.mainClass="com.microsoft.azurecat.App"  \
    -Djavax.net.debug=all \
    -Dexec.args="/home/masimms/bearer-token.json"
