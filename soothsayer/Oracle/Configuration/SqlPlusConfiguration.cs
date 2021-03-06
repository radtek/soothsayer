﻿using soothsayer.Infrastructure;
using soothsayer.Infrastructure.Configuration;

namespace soothsayer.Oracle.Configuration
{
    public class SqlPlusConfiguration : ISqlPlusConfiguration
    {
        private readonly ConfigSection _configuration;

        public SqlPlusConfiguration()
        {
            const string sectionName = "sqlPlus";
            _configuration = new ConfigSection(sectionName);
        }

        public string RunnerPath
        {
            get { return _configuration.GetConfigurationValue<string>(Name.For(() => RunnerPath)); }
        }
    }
}
