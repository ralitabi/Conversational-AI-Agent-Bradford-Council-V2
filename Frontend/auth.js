/* Bradford Council AI — Auth module
   Loaded by both login.html and index.html */

const _AUTH_KEY  = 'bca_session_v1';
const _VALID_USR = 'bradford2026';
const _VALID_PWD = '00000000';

function isAuthenticated() {
  return localStorage.getItem(_AUTH_KEY) === 'ok';
}

function doLogin(username, password) {
  if (username === _VALID_USR && password === _VALID_PWD) {
    localStorage.setItem(_AUTH_KEY, 'ok');
    return true;
  }
  return false;
}

function doLogout() {
  localStorage.removeItem(_AUTH_KEY);
  window.location.replace('login.html');
}
