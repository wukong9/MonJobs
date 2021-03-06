using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Newtonsoft.Json.Linq;

namespace MonJobs
{
    internal static class QueryExtensions
    {
        public static FilterDefinition<Job> BuildFilters(this JobQuery query)
        {
            var filters = new List<FilterDefinition<Job>>();
            var builder = Builders<Job>.Filter;
            filters.Add(builder.Eq(x => x.QueueId, query.QueueId));

            if (query.JobIds != null && query.JobIds.Any())
            {
                filters.Add(builder.In(x => x.Id, query.JobIds));
            }

            foreach (var attribute in query.HasAttributes)
            {
                var valueType = attribute.Value.GetType();
                if (typeof(IEnumerable).IsAssignableFrom(valueType) && valueType != typeof(string))
                {
                    var values = (IEnumerable)attribute.Value;

                    // Handle JArrays which always old JTokens, this unfurls the value.
                    var jArray = attribute.Value as JArray;
                    if (jArray != null)
                    {
                        values = jArray.ToObject<object[]>();
                    }

                    var orValueFilters = new List<FilterDefinition<Job>>();
                    foreach (var value in values)
                    {
                        var hasAttribute = builder.Eq(x => x.Attributes[attribute.Key], value);
                        orValueFilters.Add(hasAttribute);
                    }
                    var anyOfTheseValuesFilter = builder.Or(orValueFilters);
                    filters.Add(anyOfTheseValuesFilter);
                }
                else
                {
                    var hasAttribute = builder.Eq(x => x.Attributes[attribute.Key], attribute.Value);
                    filters.Add(hasAttribute);
                }
            }

            if (query.HasBeenAcknowledged.HasValue)
            {
                var hasBeenAcknowledged = builder.Not(builder.Eq(x => x.Acknowledgment, null));
                var hasNotBeenAcknowledged = builder.Eq(x => x.Acknowledgment, null);
                filters.Add(query.HasBeenAcknowledged.Value ? hasBeenAcknowledged : hasNotBeenAcknowledged);
            }

            if (query.HasResult.HasValue)
            {
                var hasFinished = builder.Not(builder.Eq(x => x.Result, null));
                var hasNoResults = builder.Eq(x => x.Result, null);
                filters.Add(query.HasResult.Value ? hasFinished : hasNoResults);
            }

            if (!string.IsNullOrWhiteSpace(query.AdhocQuery))
            {

                BsonDocument bsonDocumentFilter;
                try
                {
                    bsonDocumentFilter = BsonSerializer.Deserialize<BsonDocument>(query.AdhocQuery);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Invalid adhocQuery ({query.AdhocQuery})", ex);
                }
                filters.Add(bsonDocumentFilter);
            }

            if (!(query.AdhocFilter == null))
            {
                filters.Add(query.AdhocFilter);
            }

            var matchesAllFilters = Builders<Job>.Filter.And(filters);
            return matchesAllFilters;
        }

    }
}