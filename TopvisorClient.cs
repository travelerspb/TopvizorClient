using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ITA.Topvisor
{
    public sealed class TopvisorClient
    {
        //apiKey = ConfigurationManager.AppSettings["topvizor.api.key"]; ToDo
        private const string BaseAddress = "http://api.topvisor.ru/";
        private const int Delay = 500;
        private const int PerPage = 100;
        private readonly string _apiKey;

        public TopvisorClient(string apiKey)
        {
            _apiKey = apiKey;
        }

        public IEnumerable<ProjectInfo> GetProjectsList()
        {
            var getProjectsParams = new TopvisorBaseRequestParams
            {
                Oper = oper.get,
                Module = module.mod_projects,
            };

            dynamic answer = PrepareAndGetAnswerAsync(getProjectsParams).Result;

            var projects = new List<ProjectInfo>();
            foreach (dynamic proj in answer)
            {
                var item = new ProjectInfo
                {
                    Id = proj["id"],
                    Name = proj["name"],
                    UpdateDateTime = proj["update"] == "0000-00-00 00:00:00" ? null : proj["update"],
                    Status = proj["status"]
                };
                projects.Add(item);
            }

            return projects.ToArray();
        }

        public int AddProject(string url)
        {
            var getProjectsParams = new TopvisorBaseRequestParams
            {
                Oper = oper.add,
                Module = module.mod_projects,
            };
            var post = new Dictionary<string, string>
            {
                {"site", url},
                {"on", 1.ToString()},
                {"time_for_update", ":6"}
            };

            dynamic answer = PrepareAndGetAnswerAsync(getProjectsParams, post.ToArray()).Result;
            if ((bool)answer.error) 
                throw new Exception(string.Format("Ошибка добавления проекта с сообщением '{0}'",
                    answer.message));
            return (int)answer.result;
        }

        /// <summary>
        /// Добавляем ПС к текущему проекту
        /// </summary>
        /// <param name="project"></param>
        /// <param name="se"></param>
        /// <returns></returns>
        public int AddSearcher(int project, SearchEngines se)
        {
            var getProjectsParams = new TopvisorBaseRequestParams
            {
                Oper = oper.add,
                Module = module.mod_projects,
                Method = method.searcher,
            };
            var post = new Dictionary<string, string>
            {
                { "project_id", project.ToString() },
                { "searcher", ((int)se).ToString() },
            };

            dynamic answer = PrepareAndGetAnswerAsync(getProjectsParams, post.ToArray()).Result;
            if ((bool)answer.error)
                throw new ApplicationException(string.Format(
                    "Ошибка добавления ПС с сообщением '{0}'", answer.message));
            return answer.result.id;
        }

        public bool AddRegionToSearcher(int searcher, int region)
        {
            var getProjectsParams = new TopvisorBaseRequestParams
            {
                Oper = oper.add,
                Module = module.mod_projects,
                Method = method.searcher_region,
            };
            var post = new Dictionary<string, string>
            {
                { "searcher_id", searcher.ToString() },
                { "region", region.ToString() },
            };

            dynamic answer = PrepareAndGetAnswerAsync(getProjectsParams, post.ToArray()).Result;
            if ((bool)answer.error)
                throw new ApplicationException(string.Format(
                    "Ошибка добавления региона с сообщением '{0}'", answer.message));
            return true;
        }

        public bool DeleteRequest(int request)
        {
            var getProjectsParams = new TopvisorBaseRequestParams
            {
                Oper = oper.del,
                Module = module.mod_phrases,
            };
            var post = new Dictionary<string, string>
            {
                { "id", request.ToString() },
            };

            dynamic answer = PrepareAndGetAnswerAsync(getProjectsParams, post.ToArray()).Result;
            if ((bool)answer.error)
                throw new ApplicationException(string.Format(
                    "Ошибка удаления запроса '{0}'", answer.message));
            return true;
        }

        public IEnumerable<TopvisorPhraseStat> GetPhrasesHistory(int projectId, SearchEngines sEngine, int regionId,
            DateTime dateFrom, DateTime dateTo)
        {
            var getHistoryOptions = new TopvisorBaseRequestParams
            {
                Oper = oper.get,
                Module = module.mod_phrases,
                Method = method.history,
            };
            int counter = 1;
            var post = new Dictionary<string, string>
            {
                {"page", ""},
                {"project_id", projectId.ToString()},
                {"rows", PerPage.ToString()},
                {"searcher_data", sEngine.ToString("D") + "|"},
                {"region_key", regionId.ToString()},
                {"group_is", (-1).ToString()},
                {"date1", dateFrom.ToString("yyyy-MM-dd")},
                {"date2", dateTo.ToString("yyyy-MM-dd")},
                {"type_range", 2.ToString()},
            };
            var allStat = new List<TopvisorPhraseStat>();

            while (true)
            {
                post["page"] = counter.ToString();
                dynamic answer = PrepareAndGetAnswerAsync(getHistoryOptions, post.ToArray()).Result;

                dynamic dates = answer.all_dates.ToObject<DateTime[]>();

                if (dates.Length == 0) break; // Проверка, есть ли данные вообще


                var datePhraseStat = new List<TopvisorPhraseStat>();
                foreach (JToken phrase in answer.phrases)
                {
                    int iterator = 0;
                    foreach (JToken stat in phrase["dates"].Where(x => x.Value<string>("id") != "--"))
                        // Даты нужно отбирать 
                    {
                        var newStat = new TopvisorPhraseStat
                        {
                            ToVisId = phrase.Value<int>("id"),
                            Date = dates[iterator],
                            Position = stat.Value<int?>("position"),
                            Text = phrase.Value<string>("phrase"),
                            Url = stat.Value<string>("page"),
                        };
                        datePhraseStat.Add(newStat);
                        iterator++;
                    }
                }
                allStat.AddRange(datePhraseStat);
                if (counter*PerPage <= answer.Value<int>("total"))
                {
                    counter++;
                    continue;
                }
                break;
            }
            return allStat.ToArray();
        }

        /// <summary>
        ///     Выгружаем фразы в проект. Импортируется только разница
        /// </summary>
        /// <param name="progectId"></param>
        /// <param name="phrases"></param>
        /// <returns>Колв-во импортированных фраз</returns>
        public int ImportPhrases(int progectId, IEnumerable<Phrase> phrases)
        {
            var importOptions = new TopvisorBaseRequestParams
            {
                Oper = oper.add,
                Module = module.mod_phrases,
                Method = method.import,
            };
            var post = new Dictionary<string, string>
            {
                {"project_id", progectId.ToString()},
                {"phrases", String.Join("|||", phrases.Select(x => x.Text))}
            };

            dynamic answer = PrepareAndGetAnswerAsync(importOptions, post.ToArray()).Result;
            return answer.result;
        }

        /// <summary>
        ///     Получаем список фраз проекта
        /// </summary>
        /// <param name="projectId">Id проекта в системе TopVisor</param>
        /// <param name="onlyActive"></param>
        /// <returns></returns>
        public IEnumerable<Phrase> GetProjectPhrases(int projectId, bool onlyActive = true)
        {
            var getProjectParams = new TopvisorBaseRequestParams
            {
                Oper = oper.get,
                Module = module.mod_phrases,
            };

            string postParam = String.Format("&post[project_id]={0}&post[only_enabled]={1}", projectId,
                onlyActive ? 1 : 0);

            var post = new Dictionary<string, string>
            {
                {"project_id", projectId.ToString()},
                {"only_enabled", onlyActive ? 1.ToString() : 0.ToString()}
            };

            string answer = PrepareAndGetAnswerAsync(getProjectParams, post.ToArray()).Result.ToString();
            var result = JsonConvert.DeserializeObject<JToken>(answer);
            
            return result.Select(phrase => new Phrase
            {
                Id = phrase.Value<int>("id"),
                Text = phrase.Value<string>("phrase")
            }).ToArray();
        }

        /// <summary>
        ///     Подгоавливаем запрос из параметров, проверяем готовность и заплашиваем
        /// </summary>
        /// <param name="baseParams">Базовые параметры</param>
        /// <param name="postParams">Доп post параметры</param>
        /// <returns>Десериализованный ответ от сервера</returns>
        private async Task<dynamic> PrepareAndGetAnswerAsync(TopvisorBaseRequestParams baseParams,
            params KeyValuePair<string, string>[] postParams)
        {
            var uriBuilder = new UriBuilder(BaseAddress);
            using (var client = new HttpClient())
            {
                uriBuilder.Query = String.Format("api_key={0}{1}", _apiKey, baseParams);

                Thread.Sleep(Delay); // Требования сервера - не более 1 запроса в 0.1сек
                HttpResponseMessage response =
                    await client.PostAsync(uriBuilder.Uri, new FormUrlEncodedContent(postParams));

                if (response.IsSuccessStatusCode)
                {
                    string answer = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<dynamic>(answer, new JsonSerializerSettings());
                }

                throw new Exception(response.StatusCode.ToString());
            }
        }

        /// <summary>
        ///     Собираем Url для запроса
        /// </summary>
        /// <param name="baseParams">Базовые парамтеры</param>
        /// <param name="postString">Строка post</param>
        /// <param name="otherParams">фильры и прочее</param>
        /// <returns>Url строка для подключения к ТопВизору</returns>
        private Uri UrlConstructor(TopvisorBaseRequestParams baseParams, string postString = null,
            string otherParams = null)
        {
            var sb = new StringBuilder();
            sb.AppendFormat("{0}{1}{2}", BaseAddress, _apiKey, baseParams);

            if (!String.IsNullOrEmpty(postString))
                sb.Append(postString);
            if (!String.IsNullOrEmpty(otherParams))
                sb.Append(otherParams);

            try
            {
                var url = new Uri(sb.ToString());
                return url;
            }
            catch (UriFormatException exception)
            {
                throw new ArgumentException("Неверная строка для Url: ", exception.Message);
            }
        }

        /// <summary>
        ///     Ручной запуск проверки позиций
        /// </summary>
        /// <param name="projectId">Id Проекта</param>
        public void StartPositionsCheck(int projectId)
        {
            var getProjectsParams = new TopvisorBaseRequestParams
            {
                Oper = oper.edit,
                Module = module.mod_phrases,
                Method = method.parse_task,
            };
            var post = new Dictionary<string, string>
            {
                {"id", projectId.ToString()},
            };

            dynamic answer = PrepareAndGetAnswerAsync(getProjectsParams, post.ToArray()).Result;
        }

        /// <summary>
        ///     Проверяем статус проверки позиций проекта
        /// </summary>
        /// <param name="projectId"></param>
        /// <returns>Проверка закончена?</returns>
        public bool CheckProjectStatus(int projectId)
        {
            var getProjectsParams = new TopvisorBaseRequestParams
            {
                Oper = oper.get,
                Module = module.mod_phrases,
                Method = method.percent_of_parse,
            };

            var post = new Dictionary<string, string>
            {
                {"project_ids[]", projectId.ToString() },
            };

            dynamic answer = PrepareAndGetAnswerAsync(getProjectsParams, post.ToArray()).Result;
            return answer[0].percent == "100";
        }

        public void DeleteProject(int p)
        {
            var getProjectsParams = new TopvisorBaseRequestParams
            {
                Oper = oper.del,
                Module = module.mod_projects,
            };

            var post = new Dictionary<string, string>
            {
                {"id", p.ToString()},
            };

            dynamic answer = PrepareAndGetAnswerAsync(getProjectsParams, post.ToArray()).Result;
        }
    }

    internal class TopvisorBaseRequestParams
    {
        public oper Oper { set; private get; }
        public module Module { set; private get; }
        public method Method { set; private get; }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendFormat("&oper={0}&module={1}", Oper, Module);
            if (Method != 0)
                sb.AppendFormat("&method={0}", Method);
            return sb.ToString();
        }
    }
}