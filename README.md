# Rock RMS Plugin Migration Rollback

**Warning: this is a developer only tool. You DEFINITELY shouldn't run this on a production server.**

This project provides a block for [Rock RMS](https://www.rockrms.com/) that lets developers easily roll back database migrations in plugins. This is intended to be used during plugin development for testing and tweaking.

All plugin migrations which have been applied to the Rock database will be shown in a table. Each one has a simple "rollback" button next it. Rolling back multiple migrations at once are supported; if you try to rollback a migration that's been followed by other migrations, both the target migration and its dependents will be rolled back (in reverse chronological order).

Rock will automatically reapply any missing migrations on its next startup.

## Screenshots

![Migrations grid view](screenshot-1.png?raw=true "Migrations grid view")
![Rolling back migrations](screenshot-2.png?raw=true "Rolling back migrations")

## How to use

1. Copy the `MigrationsList.ascx` and `MigrationsList.ascx.cs` files to `RockWeb\Plugins\digital_am` in your Rock installation. You'll need to create the `digital_am` folder.
2. Add the block to a page in Rock. It'll be under `digital_am > Plugin Migrations > Plugin Migrations List`. Adding it to a child page of `Admin Tools > Power Tools` might be a good spot.

## Credits

Created by [AM Digital Agency](http://am.digital).