import json
from pprint import pprint


# Parse the network usages file
with open('network.json') as f:
    network = json.load(f)

for record in network:
    quotaName = record["id"].split("/")[-1]
    print('{0},{1},{2}'.format(quotaName, record["limit"], record["localName"]))
    
