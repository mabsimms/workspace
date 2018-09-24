#!/usr/bin/python

import os
import json
from azure.common.credentials import ServicePrincipalCredentials
from azure.mgmt.resource import ResourceManagementClient

subscription_id = os.environ.get('AZURE_SUBSCRIPTION_ID')
credentials = ServicePrincipalCredentials(
    client_id=os.environ['AZURE_CLIENT_ID'],
    secret=os.environ['AZURE_CLIENT_SECRET'],
    tenant=os.environ['AZURE_TENANT_ID']
)
resource_client = ResourceManagementClient(credentials, subscription_id)

