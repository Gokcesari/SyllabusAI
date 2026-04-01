/**
 * Sidebar profil menüsü:
 * - #userHit = tıklanınca aç/kapa
 * - #userFly = üstte açılan kutu (Çıkış burada)
 * - Sayfa dışına veya Esc ile kapanır
 */
(function () {
  function hazir() {
    var hit = document.getElementById('userHit');
    var fly = document.getElementById('userFly');
    if (!hit || !fly) return;

    function kapat() {
      fly.hidden = true;
      hit.setAttribute('aria-expanded', 'false');
    }

    hit.addEventListener('click', function (e) {
      e.stopPropagation();
      var ac = fly.hidden;
      fly.hidden = !ac;
      hit.setAttribute('aria-expanded', ac ? 'true' : 'false');
    });

    document.addEventListener('click', function () {
      kapat();
    });

    document.addEventListener('keydown', function (e) {
      if (e.key === 'Escape') kapat();
    });
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', hazir);
  } else {
    hazir();
  }
})();
