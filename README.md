# MvcBB Gallery

An image gallery mod for MvcBB, built against MvcBB's mod framework
(`MvcBB.ModKit`). Lives in its own solution, separate from MvcBB's own core
codebase - nothing in core had to change to support it.

## Architecture

- **MvcBB.Gallery.Api** - the API-side half: data access, business rules,
  and the HTTP endpoints MvcBB.App's side calls. Implements `IApiMod`, so
  MvcBB.API discovers it, registers its endpoints, and installs its database
  schema automatically once the built assembly is placed in MvcBB.API's
  `Mods/` folder.
- **MvcBB.Gallery.App** - the browsing/upload/admin UI, as a Razor Class
  Library. Implements `IAppMod`, so MvcBB.App discovers it, registers its
  controllers and precompiled views, and adds its nav link once its built
  assembly is placed in MvcBB.App's `Mods/` folder.

Both projects depend only on `MvcBB.ModKit` (plus whichever of MvcBB's own
repository/connection-factory projects they need for direct database
access) - never on MvcBB.App's or MvcBB.API's internal types directly. A few
small pieces of plumbing (file upload validation/storage, BBCode rendering)
are therefore implemented locally in this mod rather than shared, since
nothing beyond `MvcBB.ModKit` is available to reach across.

## Functionality

### Browsing
- A fixed list of admin-managed categories, shown on the gallery's home
  page alongside a "recent uploads" strip.
- Each category shows a paginated thumbnail grid of its images, sorted by
  upload date.
- Every page requires being signed in - there is no anonymous/guest access
  anywhere in this mod.

### Images
- Upload a PNG, JPG, or GIF (up to 10 MB) into a category, with a name and
  a BBCode-formatted description.
- View count, like count, and comment count are tracked as stored counters
  maintained inline on each action, not recomputed on every page load.
- An image's name, description, and category can be edited by its
  uploader, or by an Administrator or Moderator. The same two roles (plus
  the uploader) can delete it, which also removes the stored file.

### Comments and likes
- Any signed-in member can add a comment or like/unlike an image.
- Comments and likes can be removed by their own author, or by an
  Administrator or Moderator.

### Categories (admin)
- Administrators manage the category list (add, edit, delete) from a
  dedicated page. Deleting a category removes every image, comment, and
  like inside it.

## Data providers

Supports all three of MvcBB's data providers - SQL Server, PostgreSQL, and
the in-memory provider used for local development - following whichever
`DataProvider` MvcBB.API itself is configured with. Each provider has its
own repository implementation; schema for SQL Server and PostgreSQL is
created automatically (idempotently, safe to redeploy) the first time the
mod runs against a database that doesn't have it yet.

## Installing

1. Build both projects (`dotnet build MvcBB.Gallery.slnx`).
2. Copy `MvcBB.Gallery.Api.dll` into MvcBB.API's own `Mods/` folder (next to
   its other build output), and `MvcBB.Gallery.App.dll` into MvcBB.App's
   `Mods/` folder.
3. Restart both processes, so each discovers the new mod and (for the API)
   installs its schema.
4. Sign in as an Administrator, open the Mods page, and enable "Gallery".
   It can be disabled again the same way at any time, with no restart
   required either way.
