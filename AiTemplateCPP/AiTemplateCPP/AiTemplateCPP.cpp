// AiTemplateCPP.cpp : main project file.

#include "stdafx.h"
#include <stdio.h>
#include <time.h>

using namespace System;

const char* AI_TEMPLATE_REQUEST = "{\"name\":\"Microsoft.ApplicationInsights.%s.Request\",\"time\":\"%s\",\"iKey\":\"%s\",\"tags\":{\"ai.cloud.roleInstance\":\"%s\",\"ai.operation.id\":\"%s\",\"ai.internal.sdkVersion\":\"dotnet:2.4.0-32153\"},\"data\":{\"baseType\":\"RequestData\",\"baseData\":{\"ver\":2,\"id\":\"%s\",\"name\":\"%s\",\"duration\":\"%s\",\"success\":%s,\"responseCode\":\"%s\"}}}";
//const char* AI_TEMPLATE_DEPENDENCY = "{\"name\":\"Microsoft.ApplicationInsights.%s.RemoteDependency\",\"time\":\"%s\",\"iKey\":\"%s\",\"tags\":{\"ai.cloud.roleInstance\":\"%s\",\"ai.operation.parentId\":\"%s\",\"ai.operation.id\":\"%s\",\"ai.internal.sdkVersion\":\"dotnet:2.4.0-32153\"},\"data\":{\"baseType\":\"RemoteDependencyData\",\"baseData\":{\"ver\":2,\"name\":\"%s\",\"id\":\"%s\",\"data\":\"%s\",\"duration\":\"%s\",\"resultCode\":\"%s\",\"success\":%s,\"type\":\"%s\",\"target\":\"%s\"}}}";
const char* AI_TEMPLATE_DEPENDENCY = "{\"name\":\"Microsoft.ApplicationInsights.%s.RemoteDependency\",\"time\":\"%s\",\"iKey\":\"%s\",\"tags\":{\"ai.cloud.roleInstance\":\"%s\",\"ai.operation.parentId\":\"%s\",\"ai.operation.id\":\"%s\",\"ai.internal.sdkVersion\":\"dotnet:2.4.0-32153\"},\"data\":{\"baseType\":\"RemoteDependencyData\",\"baseData\":{\"ver\":2,\"name\":\"%s\",\"id\":\"%s\",\"data\":\"%s\",\"duration\":\"%s\",\"resultCode\":\"%s\",\"success\":%s,\"type\":\"%s\",\"target\":\"%s\"}}}";

void CreateRequest(char* buffer, size_t size,
	const char *iKey, const char *node_name,
	const char *operation_id, const char* activity_id,
	const char *activity_name, const char *duration,
	bool success, const char* response_code);

void CreateDependency(char* buffer, size_t size,
	const char *iKey, const char *node_name,
	const char *parent_id, const char *operation_id,
	const char *activity_id, const char *activity_name,
	const char *activity_data, const char *duration,
	const char* response_code, bool success,
	const char* type, const char* target);


int main(array<System::String ^> ^args)
{
	char aibuffer[2048];

	/*CreateRequest(aibuffer, sizeof(aibuffer), 
		"81481ee9-d1d1-42b4-9384-bc3a98e8818b", "masdev-laptop",
		"37afab06-03c7-405c-963d-66a542644b52", "TA8R5ub8rHA=",
		"DevicesAction", "00:00:01", true, "200");*/

	CreateDependency(aibuffer, sizeof(aibuffer),
		"81481ee9-d1d1-42b4-9384-bc3a98e8818b", "masdev-laptop",
		"TA8R5ub8rHA=", "operation_id", 
		"activityId", "acitivityName", 
		"https://api.twitter.com/twit",
		"00:00:01.100", "200", true, 
		"http", "api.twitter.com"
	);

	String^ msg = gcnew String(aibuffer);

    Console::WriteLine(L"{0}", msg);
    return 0;
}


void CreateDependency(char* buffer, size_t size,
	const char *iKey, const char *node_name, 
	const char *parent_id, const char *operation_id,
	const char *activity_id, const char *activity_name, 
	const char *activity_data, const char *duration, 
	const char* response_code, bool success, 
	const char* type, const char* target)
{
	time_t ts = time(NULL);
	struct tm* utc = gmtime(&ts);

	char time_buffer[32];
	snprintf(time_buffer, 32, "%04d-%02d-%02dT%02d:%02d:%02d.000Z",
		utc->tm_year + 1900, utc->tm_mon + 1, utc->tm_mday,
		utc->tm_hour, utc->tm_min, utc->tm_sec);

 	snprintf(buffer, size, AI_TEMPLATE_DEPENDENCY,
		iKey, time_buffer, iKey, node_name,
		parent_id, operation_id, activity_name, activity_id,
		activity_data, duration,
		response_code, success ? "true" : "false", 
		type, target);
}

void CreateRequest(char* buffer, size_t size, 
	const char *iKey, const char *node_name, 
	const char *operation_id, const char* activity_id,
	const char *activity_name, const char *duration,
	bool success, const char* response_code)
{	
	time_t ts = time(NULL);
	struct tm* utc = gmtime(&ts);

	char time_buffer[32];
	snprintf(time_buffer, 32, "%04d-%02d-%02dT%02d:%02d:%02d.000Z",
		utc->tm_year + 1900, utc->tm_mon + 1, utc->tm_mday,
		utc->tm_hour, utc->tm_min, utc->tm_sec);

	snprintf(buffer, size, AI_TEMPLATE_REQUEST, 
		iKey, time_buffer, iKey, node_name, operation_id, 
		activity_id, activity_name, duration,
		success ? "true" : "false", response_code);
}

