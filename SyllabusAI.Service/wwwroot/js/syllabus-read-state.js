/** Ders sayfası ve AI assistant aynı localStorage anahtarlarını kullanır. */
(function (global) {
  function scopeKey(user) {
    return String(user && (user.id || user.userId || user.email) || 'student')
      .toLowerCase()
      .replace(/[^a-z0-9._@-]+/g, '_');
  }

  function keys(user, courseId) {
    var sk = scopeKey(user);
    var id = String(courseId);
    return {
      progress: 'syllabus_read_progress_' + sk + '_' + id,
      stage: 'syllabus_read_stage_' + sk + '_' + id,
      unlock: 'syllabus_read_unlock_' + sk + '_' + id,
      legacyProgress: 'syllabus_read_progress_' + id,
      legacyStage: 'syllabus_read_stage_' + id,
      legacyUnlock: 'syllabus_read_unlock_' + id
    };
  }

  function getItem(primary, legacy) {
    return localStorage.getItem(primary) || localStorage.getItem(legacy) || null;
  }

  function isComplete(user, courseId) {
    if (!courseId) return false;
    var k = keys(user, courseId);
    var p = parseFloat(getItem(k.progress, k.legacyProgress) || '0');
    if (!isNaN(p) && p >= 0.99) return true;
    return getItem(k.unlock, k.legacyUnlock) === '1';
  }

  global.SyllabusReadState = { scopeKey: scopeKey, keys: keys, isComplete: isComplete };
})(window);
