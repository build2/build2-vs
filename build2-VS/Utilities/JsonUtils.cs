using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Diagnostics.CodeAnalysis;

namespace B2VS.Utilities
{
    internal static class JsonUtils
    {
        public static TValue StrictDeserialize<TValue>(string jsonStr, JsonSerializerOptions options = null) where TValue : class
        {
            TValue res = JsonSerializer.Deserialize<TValue>(jsonStr, options);
            if (res == null)
            {
                throw new JsonException("Unexpected null encountered");
            }
            return res;
        }
    }
}
