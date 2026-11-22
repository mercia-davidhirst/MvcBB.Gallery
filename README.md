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
small pieces of plumbing (file upload validation and storage) are therefore
implemented locally in this mod rather than shared, since nothing beyond
`MvcBB.ModKit` is available to reach across. BBCode is rendered with core's
shared `IBBCodeService`, which HTML-encodes everything before applying tags.

## Functionality

### Browsing
- A fixed list of admin-managed categories, shown on the gallery's home
  page alongside a "recent uploads" strip.
- Each category shows a paginated thumbnail grid of its images, sorted by
  upload date. The API caps a page at 50 images and the "recent" list at 50.
- Every page, and every image file, requires being signed in - there is no
  anonymous/guest access anywhere in this mod.

### Images
- Upload a PNG, JPG, or GIF (up to 10 MB) into a category, with a name and
  a BBCode-formatted description. Each member can upload up to 20 images
  per 10 minutes. Each stored file belongs to exactly one image entry.
- Image files are stored outside the public web folder (by default
  `App_Data/gallery` under MvcBB.App's content root; set
  `Gallery:StoragePath` in MvcBB.App's configuration to change it) and are
  only served to signed-in members, at `/images/gallery/<file>`. Files saved
  by earlier versions under `wwwroot/images/gallery` are moved there
  automatically when MvcBB.App starts. MvcBB.App's process needs write
  access to that folder.
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
  dedicated page.
- Deleting a category opens a confirmation page showing how many images it
  holds. The administrator either moves all of them to another category
  (the default when another category exists), or deletes them along with
  their stored files, comments, and likes.

## Data providers

Supports all three of MvcBB's data providers - SQL Server, PostgreSQL, and
the in-memory provider used for local development - following whichever
`DataProvider` MvcBB.API itself is configured with. Each provider has its
own repository implementation; schema for SQL Server and PostgreSQL is
created automatically (idempotently, safe to redeploy) the first time the
mod runs against a database that doesn't have it yet, and upgraded whenever
the mod's `Version` changes.

## Security

- **Login everywhere.** Every App page, every image file, and every API
  endpoint requires a signed-in member. The App calls the API with the
  member's own token, never a shared or service token.
- **Ownership checked by the API.** Editing or deleting an image, and
  deleting a comment, is allowed only for its author or an Administrator or
  Moderator. Category management is Administrator-only. These checks run in
  the API, not just in the UI.
- **Uploads.** Files are checked by their actual bytes (PNG, JPEG or GIF
  signature), not just the name and content type, and saved under a random
  file name with the extension of what they really are. If the API rejects
  an upload, the saved file is deleted again. Each member can make up to 20
  uploads per 10 minutes (counted in memory, so it resets when MvcBB.App
  restarts).
- **One entry per file.** The API only accepts image paths in the exact
  shape the App generates, and refuses a path another image entry already
  uses (backed by a unique index). A file is only removed from disk once no
  entry points at it, and file deletion never leaves the gallery folder.
- **Private files.** Image files are stored outside `wwwroot` and served by
  a login-only route with `X-Content-Type-Options: nosniff` and private
  caching. Anonymous requests are redirected to the login page.
- **CSRF and XSS.** Every form post carries an antiforgery token. BBCode
  descriptions and comments go through core's encoding renderer; everything
  else is encoded by Razor.

## Configuration

Optional, in MvcBB.App's configuration:

| Setting | Default | Meaning |
| --- | --- | --- |
| `Gallery:StoragePath` | `App_Data/gallery` under MvcBB.App's content root | Where uploaded image files are kept. Relative paths are resolved from the content root. Must not be inside `wwwroot`, and MvcBB.App's process needs write access. |

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

## Upgrading to 1.1.0

1.1.0 is a security release. Replace both DLLs and restart both processes.

- MvcBB.API re-runs the mod's install step because the version changed.
  It adds a unique index on the stored image path. If existing data already
  has two entries pointing at the same file, the index is skipped (the API
  still refuses duplicates for new uploads); remove the duplicates and bump
  the version again to add it.
- MvcBB.App moves any files from `wwwroot/images/gallery` into the private
  storage folder on startup. Image URLs don't change, so existing links keep
  working for signed-in members. Check the log for "Gallery: moved ..." or
  an error if the folder isn't writable.
- If a web server or CDN in front of MvcBB.App serves `/images/` directly
  from disk, exclude `/images/gallery/` so requests reach MvcBB.App.
