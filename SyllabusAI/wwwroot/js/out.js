/**
 * Sign out: clears JWT and user JSON, redirects to the login page.
 * localStorage keys must match the login flow.
 */
function out() {
  localStorage.removeItem('syllabus_token');
  localStorage.removeItem('syllabus_user');
  window.location.href = '/';
}
