using System;
using System.ComponentModel;
using System.Linq;
using System.Web.UI;
using Rock;
using Rock.Data;
using Rock.Model;
using Rock.Web.Cache;
using System.Web.UI.WebControls;
using Rock.Web.UI.Controls;
using System.Collections.Generic;
using Rock.Plugin;
using System.Data.SqlClient;

namespace RockWeb.Plugins.digital_am.PluginMigrationManagement
{
    /// <summary>
    /// Block for viewing list of plugin migrations.
    /// </summary>
    [DisplayName("Plugin Migrations List")]
    [Category("digital_am > Plugin Migrations")]
    [Description("Block for viewing and reversing all historical plugin database migrations.")]
    public partial class MigrationsList : Rock.Web.UI.RockBlock
    {
        #region Fields


        #endregion

        #region Control Methods
        /// <summary>
        /// Initializes the block controls when block is created.
        /// </summary>
        /// <param name="e"></param>
        protected override void OnInit(EventArgs e)
        {
            base.OnInit(e);

            gMigrations.DataKeyNames = new string[] { "Id" };
            gMigrations.GridRebind += gMigrations_GridRebind;
        }

        /// <summary>
        /// Populates the data in the block controls on first load.
        /// </summary>
        /// <param name="e"></param>
        protected override void OnLoad(EventArgs e)
        {
            if (!Page.IsPostBack)
            {
                BindGrid();
            }

            base.OnLoad(e);
        }

        #endregion

        #region Events
        
        /// <summary>
        /// Reloads the data in the migrations grid.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void gMigrations_GridRebind(object sender, EventArgs e)
        {
            BindGrid();
        }

        /// <summary>
        /// Shows a dialog asking the user to confirm if they'd like to roll back the specified migration(s).
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void gMigrations_ConfirmRollback(object sender, RowEventArgs e)
        {
            // determine the migrations that'll be rolled back
            int migrationDbKey = (int)e.RowKeyValue;
            PluginMigration migration = new PluginMigrationService(new RockContext()).Get(migrationDbKey);
            var migrationTypes = GetMigrationsThatCanBeRolledBack(
                migration.PluginAssemblyName,
                migration.MigrationNumber
            );

            // populate the modal's contents
            hfRollbackId.Value = migrationDbKey.ToString();
            string warning = "<p>This will rollback the following migrations:</p>";
            warning += MigrationTypesToHtmlList(migrationTypes);
            warning += "<p>Are you sure you want to do this?</p>";
            nbRollbackInfo.Title = warning;
            mdRollbackConfirm.Show();
        }

        /// <summary>
        /// Executes a rollback of the given migration (after rolling back all dependent migrations).
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        protected void mdRollbackConfirm_DoRollback(object sender, EventArgs e)
        {
            int migrationDbKey = 0;
            if (!Int32.TryParse(hfRollbackId.Value, out migrationDbKey)) {
                return;
            }
            
            PluginMigration migration = new PluginMigrationService(new RockContext()).Get(migrationDbKey);
            if (migration == null)
            {
                return;
            }

            // run the rollbacks
            var migrationTypes = GetMigrationsThatCanBeRolledBack(
                migration.PluginAssemblyName,
                migration.MigrationNumber
            );
            Exception rollbackError = null;
            try
            {
                RollbackMigrations(migrationTypes);
            }
            catch (Exception ex)
            {
                rollbackError = ex;
            }

            // reset the UI
            mdRollbackConfirm.Hide();
            hfRollbackId.Value = null;

            // show success/fail message
            if (rollbackError == null)
            {
                maRollbackStatus.Show("All rollbacks successful.", ModalAlertType.Information);
            }
            else
            {
                string error = "There was an error rolling back a migration. " +
                    "Check the exception log for more details. The migration has not been rolled back.";
                // TODO include exception message (can't right now--bug with string.EscapeQuotes() causes
                // the Rock:ModalAlert to issue a JS parse error)
                maRollbackStatus.Show(error, ModalAlertType.Warning);
            }
            
            BindGrid();
        }

        #endregion

        #region Methods

        /// <summary>
        /// Rebinds the currently-run migrations to the migrations grid.
        /// </summary>
        private void BindGrid()
        {
            var qry = new PluginMigrationService(new RockContext()).Queryable()
                .Where(m => !m.PluginAssemblyName.StartsWith("Rock"))
                .OrderBy(m => m.PluginAssemblyName)
                .ThenByDescending(m => m.MigrationNumber);

            gMigrations.DataSource = qry.ToList();
            gMigrations.EntityTypeId = EntityTypeCache.Read<PluginMigration>().Id;
            gMigrations.DataBind();

        }

        /// <summary>
        /// Rolls back each migration in the list.
        /// </summary>
        /// <param name="migrationTypes">The list of migration types to roll back.</param>
        private void RollbackMigrations(List<Type> migrationTypes)
        {
            using (SqlConnection con = GetDatabaseConnection())
            {
                // initialize the connection
                con.Open();

                try
                {
                    foreach (var migrationType in migrationTypes)
                    {
                        RollbackMigration(migrationType, con);
                    }
                }
                catch (Exception ex)
                {
                    LogException(ex);
                    throw ex;
                }
            }
        }

        /// <summary>
        /// Rolls back the given migration type. That is, executes the Down() method on the migration and
        /// updates the plugin migrations table to show the migration isn't currently applied to the database.
        /// </summary>
        /// <param name="migrationType">The migration type to roll back.</param>
        /// <param name="con">The database connection on which to execute the rollback.</param>
        private void RollbackMigration(Type migrationType, SqlConnection con)
        {
            using (var sqlTxn = con.BeginTransaction())
            {
                bool transactionActive = true;
                try
                {
                    // Create an instance of the migration and run the down action
                    var migration = Activator.CreateInstance(migrationType) as Migration;
                    migration.SqlConnection = con;
                    migration.SqlTransaction = sqlTxn;
                    migration.Down();
                    sqlTxn.Commit();
                    transactionActive = false;

                    // Remove the plugin migration record to indicate that this migration isn't currently applied
                    var rockContext = new RockContext();
                    var pluginMigrationService = new PluginMigrationService(rockContext);
                    string assemblyName = migrationType.Assembly.GetName().Name;
                    int migrationNumber = GetMigrationNumberForMigrationType(migrationType).Number;
                    string migrationName = migrationType.Name;
                    var migrationRecord = pluginMigrationService
                        .Queryable()
                        .Where(pm => pm.PluginAssemblyName == assemblyName)
                        .Where(pm => pm.MigrationNumber == migrationNumber)
                        .Where(pm => pm.MigrationName == migrationName)
                        .First();
                    pluginMigrationService.Delete(migrationRecord);
                    rockContext.SaveChanges();
                }
                catch (Exception ex)
                {
                    if (transactionActive)
                    {
                        sqlTxn.Rollback();
                    }
                    throw ex;
                }
            }
        }

        /// <summary>
        /// Generates a simple unordered list describing all of the given migration types.
        /// </summary>
        /// <param name="types">The migration types to include in the list.</param>
        /// <returns>HTML consisting of an unordered list.</returns>
        private static string MigrationTypesToHtmlList(List<Type> types)
        {
            string html = "<ul>";
            foreach (Type type in types)
            {
                int migrationNumber = GetMigrationNumberForMigrationType(type).Number;
                string migrationName = type.Name;
                string assemblyName = type.Assembly.GetName().Name;
                string listItem = string.Format("<li>{0} #{1}: {2}</li>", assemblyName, migrationNumber, migrationName);
                html += listItem;
            }
            html += "</ul>";
            return html;
        }

        /// <summary>
        /// Gets a list of all the plugin migrations for the given plugin which are currently
        /// applied to the database.
        /// </summary>
        /// <param name="assemblyName">The assembly name of the plugin to check</param>
        /// <returns>The plugin migration records.</returns>
        private List<PluginMigration> GetMigrationsAlreadyRun(string assemblyName)
        {
            return new PluginMigrationService(new RockContext())
                .Queryable()
                .Where(pm => pm.PluginAssemblyName == assemblyName)
                .OrderBy(pm => pm.MigrationNumber)
                .ToList();
        }

        /// <summary>
        /// Gets a list of migration types for the given plugin that are currently applied to the
        /// database which have happened since the given migration number (including the migration
        /// with that number).
        /// </summary>
        /// <param name="assemblyName">The assembly name of the plugin.</param>
        /// <param name="sinceMigration">The earliest migration number to include in the list.</param>
        /// <returns>The migration type classes.</returns>
        private List<Type> GetMigrationsThatCanBeRolledBack(string assemblyName, int sinceMigration)
        {
            List<Type> migrationTypes = GetMigrationTypes(assemblyName, sinceMigration);
            List<PluginMigration> migrationsRun = GetMigrationsAlreadyRun(assemblyName);
            List<Type> migrationsCanRollback = new List<Type>();

            // remove the intersection of these two lists
            foreach (Type migrationType in migrationTypes)
            {
                MigrationNumberAttribute mNumber = GetMigrationNumberForMigrationType(migrationType);
                bool alreadyRun = migrationsRun.Where(pm => pm.MigrationNumber == mNumber.Number).Any();
                if (alreadyRun)
                {
                    migrationsCanRollback.Add(migrationType);
                }
            }

            return migrationsCanRollback;
        }

        /// <summary>
        /// Gets the migration classes for the given plugin that are after (or including) the
        /// given migration number.
        /// </summary>
        /// <param name="assemblyName">The name of the plugin assembly.</param>
        /// <param name="sinceMigration">The earliest migration number to include in the list.</param>
        /// <returns>The migration type classes.</returns>
        private List<Type> GetMigrationTypes(string assemblyName, int sinceMigration)
        {
            return GetMigrationTypes(assemblyName)
                .Where(mt => GetMigrationNumberForMigrationType(mt).Number >= sinceMigration)
                .Reverse()
                .ToList();
        }

        /// <summary>
        /// Gets all migration type classes for the given plugin.
        /// </summary>
        /// <param name="assemblyName">The name of the plugin assembly.</param>
        /// <returns>The migration type classes.</returns>
        private List<Type> GetMigrationTypes(string assemblyName)
        {
            List<Type> assemblyMigrationTypes = GetMigrationTypes()
                .Where(mt => mt.Assembly.GetName().Name == assemblyName)
                .ToList();
            
            return assemblyMigrationTypes
                .OrderBy(mt => GetMigrationNumberForMigrationType(mt).Number)
                .ToList();
        }

        /// <summary>
        /// Gets all plugin migration classes that exist within Rock.
        /// </summary>
        /// <returns>The migration type classes.</returns>
        private List<Type> GetMigrationTypes()
        {
            // based on process in RockWeb/App_Code/Global.asax.cs
            return Reflection.FindTypes(typeof(Migration)).Select(a => a.Value).ToList();
        }

        /// <summary>
        /// Gets a new database connection instance for the main Rock database.
        /// </summary>
        /// <returns>A database connection instance.</returns>
        private SqlConnection GetDatabaseConnection()
        {
            var configConnectionString = System.Configuration.ConfigurationManager.ConnectionStrings["RockContext"];
            if (configConnectionString == null)
            {
                return null;
            }

            string connectionString = configConnectionString.ConnectionString;
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return null;
            }

            return new SqlConnection(connectionString);
        }

        /// <summary>
        /// Gets the migration number attribute from a migration type class.
        /// </summary>
        /// <param name="migrationType">The migration type to examine.</param>
        /// <returns>A migration number defined on the class.</returns>
        private static MigrationNumberAttribute GetMigrationNumberForMigrationType(Type migrationType)
        {
            return System.Attribute.GetCustomAttribute(migrationType, typeof(MigrationNumberAttribute)) as MigrationNumberAttribute;
        }

        #endregion

    }
}