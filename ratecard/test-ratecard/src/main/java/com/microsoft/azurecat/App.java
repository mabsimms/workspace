package com.microsoft.azurecat;

import java.io.IOException;
import java.io.InputStream;
import java.io.InputStreamReader;
import java.net.MalformedURLException;
import java.net.URI;
import java.net.URL;
import java.net.URLEncoder;
import java.nio.charset.Charset;
import java.nio.file.Paths;

import javax.net.ssl.HttpsURLConnection;

import com.google.common.io.CharStreams;
import com.google.gson.Gson;

import org.apache.commons.io.IOUtils;
import org.apache.http.client.utils.URIBuilder;
import org.apache.http.client.utils.URIUtils;

/**
 * Hello world!
 *
 */
public class App 
{
    public static void main( String[] args )
    {
        try 
        {
            URL path = Paths.get(args[0]).toUri().toURL();
            System.out.println("Loading token file from " + path.toString());
            BearerToken token = getAzureApplicationCredentialToken(path);
            System.out.println("Bearer token is " + token.accessToken);
        
            String url = getRateCardUrl(
                "3e9c25fc-55b3-4837-9bba-02b6eb204331", 
                "2015-06-01-preview",
                "MS-AZR-0003P",
                "USD",
                "en-US",
                "US"
                );

            Run(url, token.accessToken);

            System.out.println(url);
        }
        catch (Exception ex) 
        { 
            System.out.println(ex.toString());
        }
    }

    public static String Run(String rateCardUrl, String token) 
        throws MalformedURLException, IOException
    { 
        URL requestURL = new URL(rateCardUrl);
        HttpsURLConnection httpsUrlConnection = (HttpsURLConnection) requestURL.openConnection();
        httpsUrlConnection.setRequestProperty("Accept-Charset", "UTF-8");        
        httpsUrlConnection.setRequestProperty("Authorization", "Bearer " + token);
        httpsUrlConnection.connect();

        System.out.println("Downloading rate card from " + rateCardUrl);

        try (final InputStreamReader in = new InputStreamReader(
            (InputStream)httpsUrlConnection.getContent())) 
        {
            String text = CharStreams.toString(in);
            return text;            
        }        
    }

    public static BearerToken getAzureApplicationCredentialToken(URL tokenFile) throws IOException
    { 
        Gson gson = new Gson();
        String tokenContent = IOUtils.toString(tokenFile, Charset.defaultCharset());
        BearerToken token = gson.fromJson(tokenContent, BearerToken.class);
        return token;
    }

    public static String getRateCardUrl(String subscriptionId, String apiVersion,
        String offerDurableId, String currency, String locale, String region) 
    {
        try 
        {
            String path = String.format("subscriptions/%s/providers/Microsoft.Commerce/RateCard", subscriptionId);

            //String filter = "OfferDurableId+eq+%27MS-AZR-0003P%27+and+Currency+eq+%27USD%27+and+Locale+eq+%27en-US%27+and+RegionInfo+eq+%27US%27";
            String filter = "OfferDurableId eq 'MS-AZR-0121p' and Currency eq 'USD' and Locale eq 'en-US' and RegionInfo eq 'US'";

            // String filter = String.format("OfferDurableId eq '%s' and Currency eq '%s' and Locale eq '%s' and RegionInfo eq '%s'",
            //     offerDurableId, currency, locale, region);
           // String encoded = URLEncoder.encode(filter, "UTF-8");

            URIBuilder builder = new URIBuilder()
                .setScheme("https")
                .setHost("management.azure.com")
                .setPath(path)
                .addParameter("api-version", apiVersion)
                .addParameter("$filter", filter)                
                ;

            URI url = builder.build();
            return url.toString();
        }
        catch (Exception ex) 
        { 

        }
        return "";
    }
}
