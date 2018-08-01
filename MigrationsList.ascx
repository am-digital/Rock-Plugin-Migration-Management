<%@ Control Language="C#" AutoEventWireup="true" CodeFile="MigrationsList.ascx.cs" Inherits="RockWeb.Plugins.digital_am.PluginMigrationManagement.MigrationsList" %>

<asp:UpdatePanel ID="upMigrationsList" runat="server">
    <ContentTemplate>

        <Rock:ModalDialog ID="mdRollbackConfirm" runat="server" Title="Confirm Rollback" SaveButtonText="Rollback" OnSaveClick="mdRollbackConfirm_DoRollback">
            <Content>
                <asp:HiddenField ID="hfRollbackId" runat="server" />
                <Rock:NotificationBox ID="nbRollbackInfo" runat="server" NotificationBoxType="Warning" />
            </Content>
        </Rock:ModalDialog>
        
        <Rock:ModalAlert ID="maRollbackStatus" runat="server" />

        <div class="panel panel-block">
            <div class="panel-heading">
                <h1 class="panel-title"><i class="fa fa-database"></i> Plugin Migrations</h1>
            </div>
            <div class="panel-body">

                <div class="grid grid-panel">
                    <Rock:Grid ID="gMigrations" runat="server" RowItemText="Migration" TooltipField="Description">
                        <Columns>
                            <Rock:RockBoundField DataField="Id" HeaderText="Id" Visible="false" />
                            <Rock:RockBoundField DataField="PluginAssemblyName" HeaderText="Plugin" />
                            <Rock:RockBoundField DataField="MigrationNumber" HeaderText="Migration #" />
                            <Rock:RockBoundField DataField="MigrationName" HeaderText="Migration Name" />
                            <Rock:EditField OnClick="gMigrations_ConfirmRollback" IconCssClass="fa fa-undo" HeaderText="Rollback" ToolTip="Rollback" />
                        </Columns>
                    </Rock:Grid>
                </div>

            </div>
        </div>

    </ContentTemplate>
</asp:UpdatePanel>
