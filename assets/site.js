document.getElementById('yr').textContent = new Date().getFullYear();

// Highlight the nav link matching the current page.
(function () {
  var path = location.pathname.replace(/\/index\.html$/, '/').replace(/\.html$/, '');
  if (path === '') path = '/';
  document.querySelectorAll('.navlinks a[data-path]').forEach(function (a) {
    if (a.getAttribute('data-path') === path) a.classList.add('active');
  });
})();

// Reveal-on-scroll for anything marked .reveal.
(function () {
  var els = document.querySelectorAll('.reveal');
  if (!('IntersectionObserver' in window) || !els.length) {
    els.forEach(function (el) { el.classList.add('visible'); });
    return;
  }
  var io = new IntersectionObserver(function (entries) {
    entries.forEach(function (e) {
      if (e.isIntersecting) { e.target.classList.add('visible'); io.unobserve(e.target); }
    });
  }, { threshold: 0.15, rootMargin: '0px 0px -40px 0px' });
  els.forEach(function (el) { io.observe(el); });
})();
