/**
 * Sidebar profile menu:
 * - #userHit toggles open/closed
 * - #userFly is the popover (Sign out)
 * - Closes on outside click or Escape
 */
(function () {
  function ready() {
    var hit = document.getElementById('userHit');
    var fly = document.getElementById('userFly');
    if (!hit || !fly) return;

    function closeMenu() {
      fly.hidden = true;
      hit.setAttribute('aria-expanded', 'false');
    }

    hit.addEventListener('click', function (e) {
      e.stopPropagation();
      var open = fly.hidden;
      fly.hidden = !open;
      hit.setAttribute('aria-expanded', open ? 'true' : 'false');
    });

    document.addEventListener('click', function () {
      closeMenu();
    });

    document.addEventListener('keydown', function (e) {
      if (e.key === 'Escape') closeMenu();
    });
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', ready);
  } else {
    ready();
  }
})();
