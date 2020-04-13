using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;
using System;
using System.Threading.Tasks;

namespace AspNetCoreRateLimit
{
    public class RedisCacheRateLimitCounterStore : IRateLimitCounterStore
    {
        static private readonly LuaScript _atomicIncrement = LuaScript.Prepare("local count count = redis.call(\"INCR\", @key) if tonumber(count) == 1 then redis.call(\"EXPIRE\", @key, @timeout) end return count");
        private readonly IConnectionMultiplexer _redis;

        public RedisCacheRateLimitCounterStore(IConnectionMultiplexer redis)
        {
            _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        }

        public async Task<RateLimitCounter> IncrementAsync(string counterId, TimeSpan interval, Func<double> RateIncrementer = null)
        {
            var now = DateTime.UtcNow;
            var intervalNumber = now.Ticks / interval.Ticks;
            var intervalStart = new DateTime(intervalNumber * interval.Ticks, DateTimeKind.Utc);

            // Call the Lua script
            var count = await _redis.GetDatabase().ScriptEvaluateAsync(_atomicIncrement, new { key = counterId, timeout = interval.TotalSeconds });
            return new RateLimitCounter
            {
                Count = (double)count,
                Timestamp = intervalStart
            };
        }
    }
}