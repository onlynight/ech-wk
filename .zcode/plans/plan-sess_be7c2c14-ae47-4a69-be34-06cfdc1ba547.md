## Plan: Default-select first server on HomePage

### Current state
`RefreshServerList()` already sets `SelectedIndex = 0` when servers exist, and the `SelectionChanged` handler updates `_selectedServer` and calls `UpdateNodeInfo`. However, there's a race condition: `SelectedItem` may be null immediately after `SelectedIndex = 0` on an `ObservableCollection` because the ComboBox processes the item binding asynchronously.

### Change
In `RefreshServerList()`, after setting `SelectedIndex = 0`, add a fallback: if `SelectedItem` is null but the collection has items, directly grab `ServerStore.Servers[0]` and set `_selectedServer` + call `UpdateNodeInfo`. This ensures the first server is always selected and the start button uses it.