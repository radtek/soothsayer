﻿using System.Collections.Generic;
using System.Linq;
using Moq;
using NUnit.Framework;
using soothsayer.Infrastructure.IO;
using soothsayer.Migrations;
using soothsayer.Scripts;
using soothsayer.Oracle;

namespace soothsayer.Tests.Migrations
{
    [TestFixture]
    public class DownMigrationTests
    {
        public List<IStep> SomeScripts = new List<IStep> { DatabaseStep.BackwardOnly(new Script("foo", 1)), DatabaseStep.BackwardOnly(new Script("bar", 2)), DatabaseStep.BackwardOnly(new Script("baz", 3)) };

        private Mock<IDatabaseMetadataProvider> _mockMetadataProvider;
        private Mock<IVersionRespository> _mockVersionRepository;
        private Mock<IAppliedScriptsRepository> _mockAppliedScriptsRepository;
        private Mock<IScriptRunner> _mockScriptRunner;

        [SetUp]
        public void SetUp()
        {
            Prompt.ConsoleProvider = new Mock<IConsole>().Object;

            _mockMetadataProvider = new Mock<IDatabaseMetadataProvider>();
            _mockVersionRepository = new Mock<IVersionRespository>();
            _mockAppliedScriptsRepository = new Mock<IAppliedScriptsRepository>();
            _mockScriptRunner = new Mock<IScriptRunner>();
        }

        [Test]
        public void when_there_are_no_migration_scripts_then_none_are_ever_executed()
        {
            _mockMetadataProvider.Setup(m => m.SchemaExists(It.IsAny<string>())).Returns(false);

            var migration = new DownMigration(_mockMetadataProvider.Object, _mockVersionRepository.Object, _mockAppliedScriptsRepository.Object, false);
            migration.Migrate(Enumerable.Empty<IStep>(), null, null, _mockScriptRunner.Object, Some.String(), Some.String());

            _mockScriptRunner.Verify(m => m.Execute(It.IsAny<IScript>()), Times.Never);
        }

        [Test]
        public void when_schema_does_not_exist_then_do_not_run_any_migration_scripts()
        {
            _mockMetadataProvider.Setup(m => m.SchemaExists(It.IsAny<string>())).Returns(false);

            var migration = new DownMigration(_mockMetadataProvider.Object, _mockVersionRepository.Object, _mockAppliedScriptsRepository.Object, false);
            migration.Migrate(SomeScripts, null, null, _mockScriptRunner.Object, Some.String(), Some.String());

            _mockScriptRunner.Verify(m => m.Execute(It.IsAny<IScript>()), Times.Never);
        }

        [Test]
        public void when_schema_exists_but_there_are_no_recorded_versions_then_do_not_run_any_migration_scripts()
        {
            _mockMetadataProvider.Setup(m => m.SchemaExists(It.IsAny<string>())).Returns(true);
            _mockVersionRepository.Setup(m => m.GetCurrentVersion(It.IsAny<string>())).Returns((DatabaseVersion)null);

            var migration = new DownMigration(_mockMetadataProvider.Object, _mockVersionRepository.Object, _mockAppliedScriptsRepository.Object, false);
            migration.Migrate(SomeScripts, null, null, _mockScriptRunner.Object, Some.String(), Some.String());

            _mockScriptRunner.Verify(m => m.Execute(It.IsAny<IScript>()), Times.Never);
        }

        [Test]
        public void when_schema_exists_and_there_are_recorded_versions_then_run_migration_scripts()
        {
            _mockMetadataProvider.Setup(m => m.SchemaExists(It.IsAny<string>())).Returns(true);
            _mockVersionRepository.Setup(m => m.GetCurrentVersion(It.IsAny<string>())).Returns(new DatabaseVersion(1234, Some.String()));

            var migration = new DownMigration(_mockMetadataProvider.Object, _mockVersionRepository.Object, _mockAppliedScriptsRepository.Object, false);
            migration.Migrate(SomeScripts, null, null, _mockScriptRunner.Object, Some.String(), Some.String());

            _mockScriptRunner.Verify(m => m.Execute(It.IsAny<IScript>()), Times.Exactly(3));
        }

        [Test]
        public void for_each_migration_script_downgraded_their_version_is_removed()
        {
            _mockMetadataProvider.Setup(m => m.SchemaExists(It.IsAny<string>())).Returns(true);
            _mockVersionRepository.Setup(m => m.GetCurrentVersion(It.IsAny<string>())).Returns(new DatabaseVersion(1234, Some.String()));

            var migration = new DownMigration(_mockMetadataProvider.Object, _mockVersionRepository.Object, _mockAppliedScriptsRepository.Object, false);
            migration.Migrate(SomeScripts, null, null, _mockScriptRunner.Object, Some.String(), Some.String());

            _mockVersionRepository.Verify(m => m.RemoveVersion(SomeScripts[0].BackwardScript.AsDatabaseVersion(), It.IsAny<string>()), Times.Once);
            _mockVersionRepository.Verify(m => m.RemoveVersion(SomeScripts[1].BackwardScript.AsDatabaseVersion(), It.IsAny<string>()), Times.Once);
        }

        [Test]
        public void for_each_migration_script_downgraded_then_any_applied_script_stored_is_removed()
        {
            _mockMetadataProvider.Setup(m => m.SchemaExists(It.IsAny<string>())).Returns(true);
            _mockVersionRepository.Setup(m => m.GetCurrentVersion(It.IsAny<string>())).Returns(new DatabaseVersion(1234, Some.String()));

            var migration = new DownMigration(_mockMetadataProvider.Object, _mockVersionRepository.Object, _mockAppliedScriptsRepository.Object, false);
            migration.Migrate(SomeScripts, null, null, _mockScriptRunner.Object, Some.String(), Some.String());

            _mockAppliedScriptsRepository.Verify(m => m.RemoveAppliedScript(SomeScripts[0].BackwardScript.AsDatabaseVersion(), It.IsAny<string>()), Times.Once);
            _mockAppliedScriptsRepository.Verify(m => m.RemoveAppliedScript(SomeScripts[1].BackwardScript.AsDatabaseVersion(), It.IsAny<string>()), Times.Once);
        }

        [Test]
        public void if_a_migration_script_fails_then_the_subsequent_migration_scripts_do_not_run()
        {
            _mockMetadataProvider.Setup(m => m.SchemaExists(It.IsAny<string>())).Returns(true);
            _mockVersionRepository.Setup(m => m.GetCurrentVersion(It.IsAny<string>())).Returns(new DatabaseVersion(1234, Some.String()));

            var migration = new DownMigration(_mockMetadataProvider.Object, _mockVersionRepository.Object, _mockAppliedScriptsRepository.Object, false);

            _mockScriptRunner.Setup(m => m.Execute(SomeScripts[1].BackwardScript)).Throws(new SqlPlusException());

            Ignore.Exception(() => migration.Migrate(SomeScripts, null, null, _mockScriptRunner.Object, Some.String(), Some.String()));

            _mockVersionRepository.Verify(m => m.RemoveVersion(SomeScripts[2].BackwardScript.AsDatabaseVersion(), It.IsAny<string>()), Times.Once);
            _mockVersionRepository.Verify(m => m.RemoveVersion(SomeScripts[1].BackwardScript.AsDatabaseVersion(), It.IsAny<string>()), Times.Never);
            _mockVersionRepository.Verify(m => m.RemoveVersion(SomeScripts[0].BackwardScript.AsDatabaseVersion(), It.IsAny<string>()), Times.Never);
        }

        [Test]
        public void if_force_is_specified_then_the_subsequent_migration_scripts_will_still_run()
        {
            _mockMetadataProvider.Setup(m => m.SchemaExists(It.IsAny<string>())).Returns(true);
            _mockVersionRepository.Setup(m => m.GetCurrentVersion(It.IsAny<string>())).Returns(new DatabaseVersion(1234, Some.String()));

            var migration = new DownMigration(_mockMetadataProvider.Object, _mockVersionRepository.Object, _mockAppliedScriptsRepository.Object, true);

            _mockScriptRunner.Setup(m => m.Execute(SomeScripts[1].BackwardScript)).Throws(new SqlPlusException());

            Ignore.Exception(() => migration.Migrate(SomeScripts, null, null, _mockScriptRunner.Object, Some.String(), Some.String()));

            _mockVersionRepository.Verify(m => m.RemoveVersion(SomeScripts[2].BackwardScript.AsDatabaseVersion(), It.IsAny<string>()), Times.Once);
            _mockVersionRepository.Verify(m => m.RemoveVersion(SomeScripts[1].BackwardScript.AsDatabaseVersion(), It.IsAny<string>()), Times.Once);
            _mockVersionRepository.Verify(m => m.RemoveVersion(SomeScripts[0].BackwardScript.AsDatabaseVersion(), It.IsAny<string>()), Times.Once);
        }
    }
}
