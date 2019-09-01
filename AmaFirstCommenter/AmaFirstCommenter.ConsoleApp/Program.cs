using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Configuration;
using VkNet;
using VkNet.Enums.Filters;
using VkNet.Enums.SafetyEnums;
using VkNet.Model;
using VkNet.Model.Attachments;
using VkNet.Model.RequestParams;

namespace BtsGovno.ConsoleApp
{
    internal class Program
    {
        #region Constants

        private const string VkSectionName = "vk";
        private const string AppsettingsFileName = "appsettings.json";

        private const ulong WallGetCount = 2;
        private const ulong WallGetOffset = 0;

        private const double FreshnessPeriodInMinutes = 2;
        private const int RequestTimeoutInMilliseconds = 5000;

        #endregion Constants

        #region Fields

        private static Dictionary<long, Post> _processedPosts;

        #endregion Fields

        #region Constructors

        static Program()
        {
            _processedPosts = new Dictionary<long, Post>();
        }

        #endregion Constructors

        #region Methods

        private static void Main(string[] args)
        {
            Run();
        }

        private static void Run()
        {
            ApiConfiguration apiConfiguration = GetApiConfiguration();

            var apiAuthParams = new ApiAuthParams
            {
                ApplicationId = apiConfiguration.ApplicationId,
                Login = apiConfiguration.Login,
                Password = apiConfiguration.Password,
                Settings = Settings.All
            };

            using (var api = new VkApi())
            {
                api.Authorize(apiAuthParams);
                EnsureFirstCommentCreationForNewPost(api, apiConfiguration);
            }
        }

        private static void EnsureFirstCommentCreationForNewPost(VkApi api, ApiConfiguration apiConfiguration)
        {
            while (true)
            {
                Post latestPost = GetLatestPost(api, apiConfiguration);

                if (IsPostFresh(latestPost))
                {
                    CreateCommentForLatestPost(api, apiConfiguration, latestPost);
                    _processedPosts.Add(latestPost.Id.Value, latestPost);

                    Console.WriteLine($"Comment was added to post with id {latestPost.Id}");
                }

                Thread.Sleep(RequestTimeoutInMilliseconds);
            }
        }

        private static Post GetLatestPost(VkApi api, ApiConfiguration apiConfiguration)
        {
            var wallGetParams = new WallGetParams
            {
                Domain = apiConfiguration.TargetGroup,
                Count = WallGetCount,
                Offset = WallGetOffset,
                Filter = WallFilter.All
            };

            WallGetObject getResult = api.Wall.Get(wallGetParams);
            Post latestPost = getResult.WallPosts.FirstOrDefault(x => !x.IsPinned.HasValue || !x.IsPinned.Value);

            return latestPost;
        }

        private static bool IsPostFresh(Post post)
        {
            bool result;

            if (!post.Id.HasValue || !post.Date.HasValue)
            {
                result = false;
            }
            else if (_processedPosts.ContainsKey(post.Id.Value) ||
                (DateTime.UtcNow - post.Date.Value).TotalMinutes > FreshnessPeriodInMinutes)
            {
                result = false;
            }
            else
            {
                result = true;
            }

            return result;
        }

        private static void CreateCommentForLatestPost(VkApi api, ApiConfiguration apiConfiguration, Post latestPost)
        {
            var wallCreateCommentParams = new WallCreateCommentParams
            {
                OwnerId = latestPost.OwnerId,
                PostId = latestPost.Id.Value,
                Message = apiConfiguration.TargetMessage
            };

            api.Wall.CreateComment(wallCreateCommentParams);
        }

        private static ApiConfiguration GetApiConfiguration()
        {
            IConfiguration configuration = GetConfiguration();

            var apiConfiguration = new ApiConfiguration();
            configuration.GetSection(VkSectionName).Bind(apiConfiguration);

            return apiConfiguration;
        }

        private static IConfiguration GetConfiguration()
        {
            return new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile(AppsettingsFileName)
                .Build();
        }

        #endregion Methods
    }
}
