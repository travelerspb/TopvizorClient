
namespace ITA.Topvisor
{
    /// <summary>
    /// Тип операции
    /// </summary>
    public enum oper
    {
        get,
        add,
        edit,
        del,
    }

    /// <summary>
    /// Модуль
    /// </summary>
    public enum module
    {
        mod_projects,
        mod_phrases,
    }

    /// <summary>
    /// Метод, он же операция
    /// </summary>
    public enum method
    {
        import = 1,
        /// <summary>
        /// История позиций
        /// </summary>
        history,
        /// <summary>
        /// Отправить задание на проверку позиций
        /// </summary>
        parse_task,
        /// <summary>
        /// Статус проверки
        /// </summary>
        percent_of_parse,
        /// <summary>
        /// Добавить ПС
        /// </summary>
        searcher,
        /// <summary>
        /// Регион ПС
        /// </summary>
        searcher_region,
    }

    public enum SearchEngines
    {
        Yandex,
        Google,
    }
}
