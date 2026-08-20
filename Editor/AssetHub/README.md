# README

- Originally intended to be a scene viewer for `l:scene` filter. It's now generalized to be used as an any viewer, hence the generalized name AssetHub, however the script names haven't been changed.
- Filter anything and view it the AssetHub window.
- AssetHub is fast and optimized.
- For AdditiveSceneGroups, see `Assets\_Project\Scripts\Editor\Scene\AdditiveSceneLoaderInEditor`, you create an SO out of it and simply double click to open it.
- Why not filter the project window.
    - Project window doesn't allow a folder view, AssetHub allows that, so if you have 30 scenes, you don't have all thrown on your face, you can quickly navigate between folders in which they actually exist in from the AssetHub while only seeing scenes (or anything else).
    - Duplicate project windows are buggy, navigation breaks, it navigates in both sometimes, and it often resets, so you'd have to type the filter again.
    - Why not create a scene viewer where it's just one list, and that list ordering also affects build settings order?
        - Because build settings is rarely opened. And one list is not enough when you have scenes in double-digit numbers.
    - Why not create a scenes dropdown in the toolbar?
        - Because I don't have waste time to create custom structuring in an SO.
        - Here I can simply create a scene wherever I want, and see it in this window... with the actual folder structure names.
        - No double work.
