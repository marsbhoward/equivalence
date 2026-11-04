// itch.io sends the player back here: #access_token=...&state=... (OAuth's implicit grant). The token
// is handed to the game in localStorage - same origin as /play/ - and the game trades it for a
// session (POST /auth/itch). The state must be the one the game set before it left, or the token is
// someone else's link and is dropped. Nothing is sent anywhere from this page.
(function () {
  var hash = new URLSearchParams(location.hash.replace(/^#/, ''));
  var token = hash.get('access_token') || '';
  var state = hash.get('state') || '';
  var want = '';
  try { want = localStorage.getItem('equivalence.itch.state') || ''; } catch (e) { /* storage blocked */ }
  try { localStorage.removeItem('equivalence.itch.state'); } catch (e) { /* nothing */ }
  // The token leaves the address bar before anything else.
  history.replaceState(null, '', location.pathname);
  var say = document.getElementById('say');
  if (!token || !want || state !== want) {
    say.textContent = hash.get('error') ? 'The sign-in was cancelled.' : 'That sign-in link is not this game\'s - start again from the game.';
    return;
  }
  try { localStorage.setItem('equivalence.itch.token', token); } catch (e) {
    say.textContent = 'This browser blocks storage, so the sign-in cannot reach the game.';
    return;
  }
  location.replace('/play/');
})();
