#!/usr/bin/python

import json
import datetime

from collections import namedtuple

ProviderRegistration = namedtuple('ProviderRegistration', ['namespace', 'resourceType', 'apiVersion'])

with open("providers.json", "r") as read_file:
    data = json.load(read_file)

providers=[]

for provider in data:
    namespace = provider["namespace"]
    resourceTypes = provider["resourceTypes"]
    print namespace

    for resource in resourceTypes:
        resourceType = resource["resourceType"]

#        for api in resource["apiVersions"]:
            #print "{0},{1},{2}".format(namespace, resourceType, api)
        if len(resource["apiVersions"]) > 0:            
            apiVersions=sorted(resource["apiVersions"], reverse=True)
            latestApi=max(resource["apiVersions"])                    
            prov = ProviderRegistration(namespace, resourceType, latestApi)        
            providers.append(prov)

today = datetime.datetime.now()

for provider in providers:    
    if provider.namespace.startswith('Microsoft'):
        if provider.apiVersion.endswith('-preview'):
            previewDate = datetime.datetime.strptime(provider.apiVersion[:10], '%Y-%m-%d')
            delta = today - previewDate
            print "{0},{1},{2},{3}".format(provider.namespace, provider.resourceType, provider.apiVersion, delta.days)            
        else:
            print "{0},{1},{2},{3}".format(provider.namespace, provider.resourceType, provider.apiVersion, "")            