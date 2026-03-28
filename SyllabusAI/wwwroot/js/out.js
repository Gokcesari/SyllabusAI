/**
 * Oturumu kapat: JWT + kullanıcı JSON silinir, giriş sayfasına gider.
 * localStorage anahtarları login ile aynı olmalı.
 */
function out() {
  localStorage.removeItem('syllabus_token');
  localStorage.removeItem('syllabus_user');
  window.location.href = '/';
}
