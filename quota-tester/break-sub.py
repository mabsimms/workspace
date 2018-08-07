from collections import namedtuple
from msrestazure.azure_active_directory import MSIAuthentication
from msrestazure.azure_exceptions import CloudError
from azure.common.client_factory import get_azure_cli_credentials
from azure.mgmt.resource import ResourceManagementClient, SubscriptionClient
from azure.mgmt.network import NetworkManagementClient
from pprint import pprint
from inspect import getmembers
import logging
import json
import io

class NetworkBreaker:
    logger = logging.getLogger('networking')

    maxVnetPerRange = 255
    maxVnetPerResourceGroup = 600

    def __init__(self, credentials, subscriptionId, location):
        self.network_client = NetworkManagementClient(credentials, subscriptionId)
        self.resource_client = ResourceManagementClient(credentials, subscription_id)

        self.location = location        
        self.resourceGroup = "breakvnet-rg"        

    def cleanup_resource_group(self):
        self.logger.info('Deleting resource group %s', self.resourceGroup) 
        delete_async_operation = self.resource_client.resource_groups.delete(self.resourceGroup)
        delete_async_operation.wait()
        self.logger.info('Deleted resource group %s', self.resourceGroup)

    def delete_vnet(self, index, vnet_prefix = "testvnet{}-vnet"):
        vnetId = vnet_prefix.format(index)        
        self.logger.info('Deleting virtual Network %s already created; skipping', vnetId)                    
        delete_operation = self.network_client.virtual_networks.delete(self.resourceGroup, vnetId)
        delete_operation.wait()
        self.logger.info('Deleted Network %s', vnetId)                    
        
    def create_vnet(self, index, vnet_prefix = "testvnet{}-vnet"):
        vnetId = vnet_prefix.format(index)        
        addressPrefix = "10.{}.{}.0/24".format( index//255, index%255)
        self.logger.info('Creating virtual network in resource group %s in location %s with name %s and address %s',
            self.resourceGroup, self.location, vnetId, addressPrefix)
     
        # Create the virtual network
        try:
            # Only create the vnet if it doesn't exist
            try: 
                vnetInfo = self.network_client.virtual_networks.get(self.resourceGroup, vnetId)
                self.logger.info('Virtual Network %s already created; skipping', vnetInfo)                
                return                 
            except CloudError:
                self.logger.info('Virtual Network %s does not exist; creating', vnetId) 

            async_vnet_creation = self.network_client.virtual_networks.create_or_update(
                self.resourceGroup,
                vnetId,
                {
                    'location': self.location,
                    'address_space': {
                        'address_prefixes': [addressPrefix]
                    }
                }
            )
            async_vnet_creation.wait()
            self.logger.info('Virtual Network created') 

        except CloudError as cloud_error:
            # Capture the response body and headers
            error_data = {
                'body':  cloud_error.response.text,
                'headers' : dict(cloud_error.response.headers)
            }
            self.logger.error('could not create VNET: %s; writing failure data', cloud_error) 

            # Write out the error message            
            with io.open('vnet_body.json', 'w', encoding='utf8') as vnet_body_file:
                vnet_body_file.write(cloud_error.response.text)
        
            with io.open('vnet_data.json', 'w', encoding='utf8') as vnet_data_file:
                str = json.dumps(dict(error_data), indent=True, ensure_ascii=False)
                vnet_data_file.write(str)
           
            # Signal failure
            return False

        # Success
        return True
             
     
    def break_vnet(self, target):
        # Check to see if the resource group exists (TODO - this needs to be dynamic for multiple
        # resource groups?)
        self.resource_client.resource_groups.create_or_update(
            self.resourceGroup, {'location': self.location})

        # Create the virtual networks 
        self.logger.info('Attempting to break vnets through count %s', target)
        for index in range(0, target):
            created = self.create_vnet(index)

            # If created failed, we've likely hit the quota.  Delete the last item created to leave
            # the subscription in a wonderfully fragile state
#            if not created: 
 #               self.delete_vnet(index)
  #              return 

    ########################################## Subnets ##############################################
    def create_subnet(self, index, vnet_name, subnet_prefix = "testsubnet{}-subnet"):
        subnetName = subnet_prefix.format(index)        
        addressPrefix = "10.{}.{}.0/24".format( index//255, index%255)

        self.logger.info('Creating subnet in resource group %s in location %s with name %s and address %s',
            self.resourceGroup, self.location, subnetName, addressPrefix)
     
        # Create the subnet network
        try:
            # Only create the vnet if it doesn't exist
            try: 
                subnetInfo = self.network_client.subnets.get(self.resourceGroup, vnet_name, subnetName)
                self.logger.info('Subnet %s already created; skipping', subnetInfo)                
                return                 
            except CloudError:
                self.logger.info('Subnet %s does not exist; creating', subnetName) 

            async_subnet_creation = self.network_client.subnets.create_or_update(
                self.resourceGroup,
                vnet_name,
                subnetName,
                {
                    'address_prefix': addressPrefix
                }
            )
            async_subnet_creation.wait()
            self.logger.info('Virtual Network created') 

        except CloudError as cloud_error:
            # Capture the response body and headers
            error_data = {
                'body':  cloud_error.response.text,
                'headers' : dict(cloud_error.response.headers)
            }
            self.logger.error('could not create subnet: %s; writing failure data', cloud_error) 

            # Write out the error message            
            with io.open('subnet_body.json', 'w', encoding='utf8') as vnet_body_file:
                vnet_body_file.write(cloud_error.response.text)
        
            with io.open('subnet_data.json', 'w', encoding='utf8') as vnet_data_file:
                str = json.dumps(dict(error_data), indent=True, ensure_ascii=False)
                vnet_data_file.write(str)
           
            # Signal failure
            return False

        # Success
        return True

    def delete_subnet(self, index, vnet_name, subnet_prefix = "testsubnet{}-subnet"):
        subnetName = subnet_prefix.format(index)             
        self.logger.info('Deleting subnet %s', subnetName)      
        async_subnet_deletion = self.network_client.subnets.delete(
                self.resourceGroup, vnet_name, subnetName)
        async_subnet_deletion.wait()
        self.logger.info('Deleted subnet %s ', subnetName)      

    def break_subnet(self, target):
        # Check to see if the resource group exists (TODO - this needs to be dynamic for multiple
        # resource groups?)
        self.resource_client.resource_groups.create_or_update(
            self.resourceGroup, {'location': self.location})

        try:
            vnet_name = "testsubnet-vnet"            
            async_vnet_creation = self.network_client.virtual_networks.create_or_update(
                self.resourceGroup,
                vnet_name,
                {
                    'location': self.location,
                    'address_space': {
                        'address_prefixes': ['10.0.0.0/8']
                    }
                }
            )
            async_vnet_creation.wait()
            self.logger.info('Virtual Network for subnet test created') 
        except CloudError:
            return False

        # Create the virtual networks 
        self.logger.info('Attempting to break subnets through count %s', target)
        for index in range(0, target):
            created = self.create_subnet(index, vnet_name)

            # If created failed, we've likely hit the quota.  Delete the last item created to leave
            # the subscription in a wonderfully fragile state
            if not created: 
                #self.delete_subnet(index, vnet_name)
                return 

        return True

FORMAT = '%(asctime)-15s %(message)s'
logging.basicConfig(format=FORMAT, level=logging.INFO)
logger = logging.getLogger("Main")

# Run configuration
location = "eastus"

# Set up authentication
credentials, subscription_id = get_azure_cli_credentials()



#credentials = MSIAuthentication()
#subscription_client = SubscriptionClient(credentials)
#subscription = next(subscription_client.subscriptions.list())
#subscription_id = subscription.subscription_id
logger.info("Breaking subscription id %s in location %s", subscription_id, location)

# Load in the metadata and reparse into a dictionary
ResourceInfo = namedtuple('ResourceInfo', 'name, limit')
resource_metadata = dict()
network_metadata_file = 'network.json'
compute_metadata_file = 'compute.json'

if network_metadata_file:
    with open('network.json', 'r') as f:
        raw_network_metadata = json.load(f)

        for quota in raw_network_metadata:
            id  = quota['name']['value']
            friendlyName = quota['localName']
            limit = quota['limit']
            resource_metadata[id] = ResourceInfo(friendlyName, int(limit))

#pprint(resource_metadata)

network = NetworkBreaker(credentials, subscription_id, location)
#network.cleanup_resource_group()

vnet_target = resource_metadata['VirtualNetworks'].limit + 1
network.break_vnet(vnet_target)

#subnet_target = resource_metadata['SubnetsPerVirtualNetwork'].limit + 1
#network.break_subnet(subnet_target)
