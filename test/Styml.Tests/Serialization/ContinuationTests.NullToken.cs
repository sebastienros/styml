// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace System.Text.Yaml.Serialization.Tests
{
    public static partial class ContinuationTests
    {
        private class CustomerCollectionResponse
        {
            [YamlPropertyName("customers")]
            public List<Customer> Customers { get; set; }
        }

        private class CustomerAddress
        {
            [YamlPropertyName("first_name")]
            public string FirstName { get; set; }

            [YamlPropertyName("address1")]
            public string Address1 { get; set; }

            [YamlPropertyName("phone")]
            public string Phone { get; set; }

            [YamlPropertyName("city")]
            public string City { get; set; }

            [YamlPropertyName("zip")]
            public string Zip { get; set; }

            [YamlPropertyName("province")]
            public string Province { get; set; }

            [YamlPropertyName("country")]
            public string Country { get; set; }

            [YamlPropertyName("last_name")]
            public string LastName { get; set; }

            [YamlPropertyName("address2")]
            public string Address2 { get; set; }

            [YamlPropertyName("company")]
            public string Company { get; set; }

            [YamlPropertyName("latitude")]
            public float? Latitude { get; set; }

            [YamlPropertyName("longitude")]
            public float? Longitude { get; set; }

            [YamlPropertyName("name")]
            public string Name { get; set; }

            [YamlPropertyName("country_code")]
            public string CountryCode { get; set; }

            [YamlPropertyName("province_code")]
            public string ProvinceCode { get; set; }
        }

        private class Customer
        {
            [YamlPropertyName("id")]
            public long Id { get; set; }

            [YamlPropertyName("email")]
            public string Email { get; set; }

            [YamlPropertyName("accepts_marketing")]
            public bool AcceptsMarketing { get; set; }

            [YamlPropertyName("created_at")]
            public DateTimeOffset? CreatedAt { get; set; }

            [YamlPropertyName("updated_at")]
            public DateTimeOffset? UpdatedAt { get; set; }

            [YamlPropertyName("first_name")]
            public string FirstName { get; set; }

            [YamlPropertyName("last_name")]
            public string LastName { get; set; }

            [YamlPropertyName("orders_count")]
            public int OrdersCount { get; set; }

            [YamlPropertyName("state")]
            public string State { get; set; }

            [YamlPropertyName("total_spent")]
            public string TotalSpent { get; set; }

            [YamlPropertyName("last_order_id")]
            public long? LastOrderId { get; set; }

            [YamlPropertyName("note")]
            public string Note { get; set; }

            [YamlPropertyName("verified_email")]
            public bool VerifiedEmail { get; set; }

            [YamlPropertyName("multipass_identifier")]
            public string MultipassIdentifier { get; set; }

            [YamlPropertyName("tax_exempt")]
            public bool TaxExempt { get; set; }

            [YamlPropertyName("tags")]
            public string Tags { get; set; }

            [YamlPropertyName("last_order_name")]
            public string LastOrderName { get; set; }

            [YamlPropertyName("default_address")]
            public CustomerAddress DefaultAddress { get; set; }

            [YamlPropertyName("addresses")]
            public IList<CustomerAddress> Addresses { get; set; }
        }
    }
}
